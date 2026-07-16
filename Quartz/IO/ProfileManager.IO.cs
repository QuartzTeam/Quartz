using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
namespace Quartz.IO;
public static partial class ProfileManager {
    private static string Uniquify(string name) {
        if(!Exists(name)) return name;
        for(int i = 2; ; i++) {
            string candidate = $"{name} ({i})";
            if(!Exists(candidate)) return candidate;
        }
    }
    public static string PresetsPath => Path.Combine(MainCore.Paths.RootPath, "Presets");
    public readonly struct PresetInfo {
        public readonly string Path;
        public readonly string Name;
        public PresetInfo(string path, string name) {
            Path = path;
            Name = name;
        }
    }
    public static List<PresetInfo> ListPresets() {
        List<PresetInfo> list = [];
        try {
            if(!Directory.Exists(PresetsPath)) return list;
            foreach(string ext in ImportExtensions) {
                foreach(string file in Directory.GetFiles(PresetsPath, "*." + ext)) {
                    string name = null;
                    try {
                        JToken b = JToken.Parse(File.ReadAllText(file));
                        if(IsProfileBundle(b)) name = b["Name"]?.Value<string>();
                    } catch { }
                    name = Sanitize(name) ?? Sanitize(Path.GetFileNameWithoutExtension(file));
                    if(name != null) list.Add(new PresetInfo(file, name));
                }
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] ListPresets failed: {e}");
        }
        return list;
    }
    public static string ApplyPreset(string presetPath) {
        try {
            JToken bundle = JToken.Parse(File.ReadAllText(presetPath));
            string name = Sanitize(bundle["Name"]?.Value<string>())
                ?? Sanitize(Path.GetFileNameWithoutExtension(presetPath));
            if(name == null) return null;
            if(!Exists(name)) {
                name = Import(presetPath, asPreset: true);
                if(name == null) return null;
            }
            if(name != Active) Apply(name);
            return name;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] ApplyPreset '{presetPath}' failed: {e}");
            return null;
        }
    }
    private static void CaptureTo(string name) {
        string dir = DirOf(name);
        Dictionary<string, byte[]> snapshot = new(StringComparer.OrdinalIgnoreCase);
        foreach(string file in Directory.GetFiles(MainCore.Paths.RootPath, "*.json")) {
            string fileName = Path.GetFileName(file);
            if(excluded.Contains(fileName)) continue;
            snapshot[fileName] = File.ReadAllBytes(file);
        }
        WriteProfileDirectory(dir, snapshot);
    }
    private static void WriteProfileDirectory(string directory, IReadOnlyDictionary<string, byte[]> files) {
        string parent = Path.GetDirectoryName(directory);
        if(string.IsNullOrEmpty(parent)) throw new IOException("profile directory has no parent");
        Directory.CreateDirectory(parent);
        string leaf = Path.GetFileName(directory);
        string staging = Path.Combine(parent, "." + leaf + ".stage-" + Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(parent, "." + leaf + ".old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try {
            foreach(KeyValuePair<string, byte[]> file in files) AtomicFile.WriteAllBytes(Path.Combine(staging, file.Key), file.Value);
            bool hadPrevious = Directory.Exists(directory);
            if(hadPrevious) Directory.Move(directory, backup);
            try {
                Directory.Move(staging, directory);
            } catch {
                if(hadPrevious && !Directory.Exists(directory) && Directory.Exists(backup)) Directory.Move(backup, directory);
                throw;
            }
            if(Directory.Exists(backup)) {
                try { Directory.Delete(backup, true); } catch { }
            }
        } finally {
            if(Directory.Exists(staging)) {
                try { Directory.Delete(staging, true); } catch { }
            }
        }
    }
    private static void RecoverProfileDirectories() {
        foreach(string directory in Directory.GetDirectories(ProfilesPath, ".*")) {
            string name = Path.GetFileName(directory);
            int oldMarker = name.LastIndexOf(".old-", StringComparison.Ordinal);
            int stageMarker = name.LastIndexOf(".stage-", StringComparison.Ordinal);
            if(oldMarker > 1 && IsSwapSuffix(name, oldMarker + 5)) {
                string target = DirOf(name[1..oldMarker]);
                if(!Directory.Exists(target)) Directory.Move(directory, target);
                else Directory.Delete(directory, true);
            } else if(stageMarker > 1 && IsSwapSuffix(name, stageMarker + 7)) {
                Directory.Delete(directory, true);
            }
        }
    }
    private static bool IsSwapSuffix(string name, int start)
        => start < name.Length && Guid.TryParseExact(name[start..], "N", out _);
    private static void BeginSwitch(string previous, IReadOnlyDictionary<string, string> previousFiles) {
        Dictionary<string, byte[]> rollback = previousFiles.ToDictionary(
            file => file.Key,
            file => System.Text.Encoding.UTF8.GetBytes(file.Value),
            StringComparer.OrdinalIgnoreCase
        );
        WriteProfileDirectory(SwitchRollbackPath, rollback);
        AtomicFile.WriteAllText(
            SwitchMarkerPath,
            new JObject { ["Previous"] = previous }.ToString()
        );
    }
    private static void CompleteSwitch() {
        if(File.Exists(SwitchMarkerPath)) File.Delete(SwitchMarkerPath);
        if(Directory.Exists(SwitchRollbackPath)) {
            try { Directory.Delete(SwitchRollbackPath, true); } catch { }
        }
    }
    private static void RecoverInterruptedSwitch() {
        if(!File.Exists(SwitchMarkerPath)) {
            if(Directory.Exists(SwitchRollbackPath)) Directory.Delete(SwitchRollbackPath, true);
            return;
        }
        JObject marker = JObject.Parse(File.ReadAllText(SwitchMarkerPath));
        string previous = marker.Value<string>("Previous");
        if(string.IsNullOrWhiteSpace(previous) || !Directory.Exists(SwitchRollbackPath)) {
            throw new IOException("profile switch rollback data is incomplete");
        }
        Dictionary<string, string> rollback = ReadSettingsDirectory(SwitchRollbackPath, validateJson: true);
        ReplaceLiveSettings(rollback);
        SettingsRegistry.ReloadAll();
        Active = previous;
        SavePointer();
        CompleteSwitch();
    }
    private static Dictionary<string, string> ReadSettingsDirectory(string directory, bool validateJson) {
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach(string file in Directory.GetFiles(directory, "*.json")) {
            string fileName = Path.GetFileName(file);
            if(excluded.Contains(fileName)) continue;
            string contents = File.ReadAllText(file);
            if(validateJson) JToken.Parse(contents);
            files[fileName] = contents;
        }
        return files;
    }
    private static void ReplaceLiveSettings(IReadOnlyDictionary<string, string> files) {
        foreach(string live in Directory.GetFiles(MainCore.Paths.RootPath, "*.json")) {
            string fileName = Path.GetFileName(live);
            if(!excluded.Contains(fileName) && !files.ContainsKey(fileName)) File.Delete(live);
        }
        foreach(KeyValuePair<string, string> file in files) AtomicFile.WriteAllText(Path.Combine(MainCore.Paths.RootPath, file.Key), file.Value);
    }
    private static void ApplyRuntimeSettings() {
        FontManager.SetFont(
            string.IsNullOrEmpty(MainCore.Conf.FontName)
                ? FontManager.DefaultName
                : MainCore.Conf.FontName,
            false
        );
        MainCore.Tr.Language = string.IsNullOrWhiteSpace(MainCore.Conf.Language)
            ? Translator.FALLBACK_LANGUAGE
            : MainCore.Conf.Language;
    }
    private static void SavePointer() {
        AtomicFile.WriteAllText(
            PointerPath,
            new JObject { ["Active"] = Active }.ToString()
        );
    }
}
