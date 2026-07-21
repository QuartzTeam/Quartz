using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
using Quartz.Compat.Game;
namespace Quartz.Features.KeyViewer.Layout;
internal static class KvStore {
    private const string JsonExtension = ".json";
    private const string LayoutFileName = "KeyViewerLayout.json";
    private const string CorruptPrefix = "KeyViewerLayout.corrupt-";
    private const int MaxCorruptBackups = 99;
    private const string DefaultExportName = "quartz-keyviewer-preset.json";
    private const string FilterName = "JSON Preset";
    private const long MaxImportBytes = 64L * 1024 * 1024;
    private static readonly object sync = new();
    private static readonly object requestLock = new();
    private static KvDocument current;
    private static CancellationTokenSource saveCts;
    internal static KvDocument Current {
        get {
            lock(sync) {
                current ??= Read();
                return current;
            }
        }
    }
    internal static string LayoutPath => Path.Combine(MainCore.Paths.RootPath, LayoutFileName);
    internal static void Load() {
        lock(sync) current = Read();
    }
    internal static void Replace(KvDocument doc) {
        if(doc == null) return;
        lock(sync) current = doc;
        RequestSave();
    }
    private static KvDocument Read() {
        string path;
        string json;
        try {
            path = LayoutPath;
            if(!File.Exists(path)) return KvDocument.Empty();
            json = File.ReadAllText(path);
        } catch(Exception e) {
            Err($"Failed to read the layout file: {e}");
            return KvDocument.Empty();
        }
        try {
            return KvDocument.Parse(json);
        } catch(Exception e) {
            Err($"Layout file is corrupt: {e}");
            Quarantine(path);
            return KvDocument.Empty();
        }
    }
    private static void Quarantine(string path) {
        try {
            if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string dir = Path.GetDirectoryName(path);
            if(string.IsNullOrEmpty(dir)) return;
            for(int n = 1; n <= MaxCorruptBackups; n++) {
                string backup = Path.Combine(dir, CorruptPrefix + n + JsonExtension);
                if(File.Exists(backup)) continue;
                File.Move(path, backup);
                Msg($"Preserved the corrupt layout as {backup}");
                return;
            }
            Err($"Could not preserve the corrupt layout: {MaxCorruptBackups} backups already exist.");
        } catch(Exception e) {
            Err($"Could not preserve the corrupt layout: {e}");
        }
    }
    internal static bool Save() {
        CancelPendingSave();
        return SaveCore();
    }
    private static readonly object writeLock = new();
    private static long saveSeq;
    private static long lastWrittenSeq;
    private static bool SaveCore() {
        if(!TrySerialize(out string json, out long seq)) return false;
        return WriteJson(json, seq);
    }
    private static bool TrySerialize(out string json, out long seq) {
        try {
            lock(sync) json = Current.ToJson(true);
            seq = Interlocked.Increment(ref saveSeq);
            return true;
        } catch(Exception e) {
            Err($"Failed to serialize the layout: {e}");
            json = null;
            seq = 0;
            return false;
        }
    }
    private static bool WriteJson(string json, long seq) {
        lock(writeLock) {
            if(seq < lastWrittenSeq) return true;
            try {
                AtomicFile.WriteAllText(LayoutPath, json);
                lastWrittenSeq = seq;
                return true;
            } catch(Exception e) {
                Err($"Failed to save the layout file: {e}");
                return false;
            }
        }
    }
    internal static void RequestSave(int delay = 500) {
        CancellationTokenSource request = new();
        CancellationTokenSource previous;
        lock(requestLock) {
            previous = saveCts;
            saveCts = request;
        }
        previous?.Cancel();
        _ = SaveAfterDelay(delay, request);
    }
    private static async Task SaveAfterDelay(int delay, CancellationTokenSource request) {
        CancellationToken token = request.Token;
        try {
            await Task.Delay(delay, token);
            if(token.IsCancellationRequested) {
                request.Dispose();
                return;
            }
            MainThread.Enqueue(() => {
                bool isCurrent;
                lock(requestLock) {
                    isCurrent = ReferenceEquals(saveCts, request);
                }
                if(!isCurrent || token.IsCancellationRequested) {
                    request.Dispose();
                    return;
                }
                if(SaveGate.ShouldDefer) {
                    _ = SaveAfterDelay(delay, request);
                    return;
                }
                lock(requestLock) {
                    if(ReferenceEquals(saveCts, request)) saveCts = null;
                }
                if(TrySerialize(out string json, out long seq))
                    _ = Task.Run(() => WriteJson(json, seq));
                request.Dispose();
            });
        } catch(OperationCanceledException) {
            ClearRequest(request);
        } catch(Exception e) {
            ClearRequest(request);
            Err($"Failed to request a layout save: {e}");
        }
    }
    private static void ClearRequest(CancellationTokenSource request) {
        lock(requestLock) {
            if(ReferenceEquals(saveCts, request)) saveCts = null;
        }
        request.Dispose();
    }
    internal static void CancelPendingSave() {
        CancellationTokenSource pending;
        lock(requestLock) {
            pending = saveCts;
            saveCts = null;
        }
        pending?.Cancel();
    }
    internal static bool ImportFromPath(string path, out string error) {
        error = null;
        if(string.IsNullOrWhiteSpace(path)) {
            error = "No file selected.";
            return false;
        }
        string json;
        try {
            long length = new FileInfo(path).Length;
            if(length > MaxImportBytes) {
                error = $"That file is {length / (1024 * 1024)} MB — too large to be a preset.";
                Msg(error);
                return false;
            }
            json = File.ReadAllText(path);
        } catch(Exception e) {
            error = "Import failed: " + e.Message;
            Msg(error);
            return false;
        }
        KvDocument doc;
        try {
            doc = KvDocument.Parse(json);
        } catch(FormatException e) {
            error = "Not a DM Note preset: " + e.Message;
            Msg(error);
            return false;
        } catch(Exception e) {
            error = "That file is not readable as a preset: " + e.Message;
            Msg(error);
            return false;
        }
        string added = Current.MergeFrom(doc);
        if(added != null) Current.SelectedTab = added;
        ApplyEmbeddedCss(doc);
        Save();
        Msg("Imported layout from " + path);
        return true;
    }
    private static void ApplyEmbeddedCss(KvDocument doc) {
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        if(conf == null) return;
        if(!string.IsNullOrWhiteSpace(conf.DmCssText)) return;
        (bool enabled, string content) = doc.EmbeddedCss();
        if(string.IsNullOrWhiteSpace(content)) return;
        conf.DmCssText = content;
        conf.DmCssEnabled = enabled;
        conf.DmCssPath = "";
        KeyViewerOverlay.Save();
    }
    internal static bool ExportToPath(string path, out string error, bool includeCounts = true) {
        error = null;
        if(string.IsNullOrWhiteSpace(path)) {
            error = "No destination selected.";
            return false;
        }
        try {
            string json = Current.ToJson(true);
            if(!includeCounts) json = StripCounts(json);
            AtomicFile.WriteAllText(path, json);
            Msg("Exported layout to " + path);
            return true;
        } catch(Exception e) {
            error = "Export failed: " + e.Message;
            Msg(error);
            return false;
        }
    }
    internal static bool Import(out string error) {
        error = null;
        string picked;
        try {
            picked = FileDialog.PickFile(
                "", FilterName, ["json"], "Select a DM Note preset or Quartz layout");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            Msg(error);
            return false;
        }
        return string.IsNullOrEmpty(picked) || ImportFromPath(picked, out error);
    }
    internal static bool Export(out string error, bool includeCounts = true) {
        error = null;
        string picked;
        try {
            Directory.CreateDirectory(LibraryDir);
            picked = FileDialog.SaveFile(
                LibraryDir, DefaultExportName, FilterName, ["json"], "Export KeyViewer layout");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            Msg(error);
            return false;
        }
        return string.IsNullOrEmpty(picked) || ExportToPath(EnsureJsonExtension(picked), out error, includeCounts);
    }
    private static string EnsureJsonExtension(string path) =>
        string.IsNullOrEmpty(Path.GetExtension(path)) ? path + JsonExtension : path;
    internal static void RevealLibrary() {
        try {
            Directory.CreateDirectory(LibraryDir);
            FileDialog.Reveal(LibraryDir);
        } catch(Exception e) {
            Msg("[KeyViewer] Reveal failed: " + e.Message);
        }
    }
    private static string StripCounts(string json) {
        JObject root = JObject.Parse(json);
        foreach(string table in new[] { "keyPositions", "statPositions", "graphPositions", "knobPositions" }) {
            if(root[table] is not JObject byTab) continue;
            foreach(JProperty tab in byTab.Properties()) {
                if(tab.Value is not JArray arr) continue;
                foreach(JToken entry in arr) {
                    JObject target = entry["position"] as JObject ?? entry as JObject;
                    if(target?["count"] != null) target["count"] = 0;
                }
            }
        }
        return root.ToString(Formatting.Indented);
    }
    internal static string LibraryDir => Path.Combine(MainCore.Paths.RootPath, "Presets");
    internal static string LibraryHint {
        get {
            try {
                Directory.CreateDirectory(LibraryDir);
            } catch { }
            return LibraryDir;
        }
    }
    private static string DownloadsDir {
        get {
            try {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Downloads");
            } catch {
                return null;
            }
        }
    }
    internal static IReadOnlyList<(string Path, string Name)> LibraryFiles() {
        List<(string Path, string Name)> found = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        void Scan(string dir, string tag) {
            if(string.IsNullOrEmpty(dir)) return;
            try {
                if(!Directory.Exists(dir)) return;
                foreach(string file in Directory.EnumerateFiles(dir, "*" + JsonExtension)) {
                    if(string.Equals(Path.GetFileName(file), LayoutFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    if(!seen.Add(Path.GetFullPath(file))) continue;
                    found.Add((file, tag + Path.GetFileName(file)));
                }
            } catch(Exception e) {
                Msg("[KeyViewer] Could not scan " + dir + ": " + e.Message);
            }
        }
        Scan(LibraryDir, "");
        Scan(DownloadsDir, "Downloads/");
        found.Sort((a, b) => LastWrite(b.Path).CompareTo(LastWrite(a.Path)));
        return found;
    }
    private static DateTime LastWrite(string path) {
        try {
            return File.GetLastWriteTimeUtc(path);
        } catch {
            return DateTime.MinValue;
        }
    }
    internal static bool ExportToLibrary(bool includeCounts, out string savedPath, out string error) {
        savedPath = null;
        error = null;
        try {
            Directory.CreateDirectory(LibraryDir);
            savedPath = UniqueExportPath();
        } catch(Exception e) {
            error = "Export failed: " + e.Message;
            Msg(error);
            return false;
        }
        return ExportToPath(savedPath, out error, includeCounts);
    }
    private static string UniqueExportPath() {
        string baseName = Path.GetFileNameWithoutExtension(DefaultExportName);
        string candidate = Path.Combine(LibraryDir, baseName + JsonExtension);
        for(int n = 2; File.Exists(candidate) && n < 10000; n++)
            candidate = Path.Combine(LibraryDir, baseName + "-" + n + JsonExtension);
        return candidate;
    }
    private static void Msg(string message) {
        try {
            MainCore.Log.Msg("[KeyViewer] " + message);
        } catch {
        }
    }
    private static void Err(string message) {
        try {
            MainCore.Log.Err("[KeyViewer] " + message);
        } catch {
        }
    }
}
