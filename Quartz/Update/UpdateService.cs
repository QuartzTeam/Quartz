using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Quartz.Async;
using Quartz.Core;
using Newtonsoft.Json.Linq;
namespace Quartz.Update;
public enum UpdateStatus {
    Idle,
    Checking,
    UpToDate,
    Available,
    Installing,
    Installed,
    Skipped,
    Failed,
}
public enum UpdateFailure {
    None,
    Network,
    NotFound,
    RateLimited,
    CheckError,
    InstallError,
}
public sealed class UpdateInfo {
    public string Tag;
    public string Name;
    public SemVer Version;
    public string Url;
    public string AssetUrl;
    public bool AssetIsZip;
    public string AssetSha256;
}
public static class UpdateService {
    public static UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    public static UpdateInfo Available { get; private set; }
    public static string Message { get; private set; } = "";
    public static UpdateFailure Failure { get; private set; } = UpdateFailure.None;
    public static float Progress { get; private set; } = -1f;
    public static string SkippedTag => lastSkipped?.Tag ?? MainCore.Conf.SkippedVersion;
    private static UpdateInfo lastSkipped;
    private static SemVer? installedVersion;
    public static bool DevSimulate { get; private set; }
    public static event System.Action OnChanged;
    private static readonly HttpClient Http = CreateClient();
    private sealed class CheckException : System.Exception {
        public UpdateFailure Kind { get; }
        public CheckException(UpdateFailure kind, string message) : base(message) => Kind = kind;
    }
    private static HttpClient CreateClient() {
        try {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        } catch {
        }
        HttpClient client = new() { Timeout = System.TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    private static void Set(UpdateStatus status, string message = "") {
        Status = status;
        Message = message ?? "";
        if(status != UpdateStatus.Failed) Failure = UpdateFailure.None;
        if(status != UpdateStatus.Installing) Progress = -1f;
        MainThread.Enqueue(() => OnChanged?.Invoke());
    }
    private static void Fail(UpdateFailure kind, string detail) {
        Failure = kind;
        Set(UpdateStatus.Failed, detail);
    }
    public static async void Check() {
        if(!MainCore.Host.SupportsSelfUpdate) return;
        if(Status is UpdateStatus.Checking or UpdateStatus.Installing) return;
        if(installedVersion.HasValue) {
            Set(UpdateStatus.Installed);
            return;
        }
        if(DevSimulate) {
            Available = Simulated();
            Set(UpdateStatus.Available);
            return;
        }
        Set(UpdateStatus.Checking);
        try {
            UpdateInfo found = await Task.Run(() => FetchLatest());
            Available = found;
            Set(found == null ? UpdateStatus.UpToDate : UpdateStatus.Available);
        } catch(System.Exception ex) {
            Available = null;
            Fail(Classify(ex), ex.Message);
            MainCore.Log.Wrn($"[Update] check failed: {ex.Message}");
        }
    }
    private static UpdateFailure Classify(System.Exception ex) => ex switch {
        CheckException ce => ce.Kind,
        HttpRequestException => UpdateFailure.Network,
        TaskCanceledException => UpdateFailure.Network,
        _ => UpdateFailure.CheckError,
    };
    private static async Task<UpdateInfo> FetchLatest(bool forceLatest = false) {
        string url = $"https://api.github.com/repos/{Info.RepoOwner}/{Info.RepoName}/releases?per_page=30";
        string json;
        using(HttpResponseMessage resp = await Http.GetAsync(url)) {
            if(resp.StatusCode == HttpStatusCode.NotFound)
                throw new CheckException(UpdateFailure.NotFound, "releases feed returned 404");
            if((int)resp.StatusCode is 403 or 429)
                throw new CheckException(UpdateFailure.RateLimited, $"GitHub returned {(int)resp.StatusCode}");
            resp.EnsureSuccessStatusCode();
            json = await resp.Content.ReadAsStringAsync();
        }
        JArray releases = JArray.Parse(json);
        SemVer current = Info.Current;
        string skipped = MainCore.Conf.SkippedVersion ?? string.Empty;
        UpdateInfo best = null;
        foreach(JToken rel in releases) {
            if((bool?)rel["draft"] == true) continue;
            string tag = (string)rel["tag_name"];
            if(string.IsNullOrEmpty(tag) || (!forceLatest && tag == skipped)) continue;
            if(!SemVer.TryParse(tag, out SemVer v)) continue;
            if(!MainCore.Conf.AcceptsChannel(v.Channel) || (!forceLatest && v.CompareTo(current) <= 0)) continue;
            string zipName = MainCore.Host.UpdateAssetName;
            bool allowDllFallback = zipName == "Quartz.zip";
            string zipUrl = null;
            string dllUrl = null;
            string zipSha256 = null;
            string dllSha256 = null;
            if(rel["assets"] is JArray assets) {
                foreach(JToken a in assets) {
                    string name = (string)a["name"];
                    if(name == zipName) {
                        zipUrl = (string)a["browser_download_url"];
                        zipSha256 = ParseSha256Digest((string)a["digest"]);
                    } else if(allowDllFallback && name == "Quartz.dll") {
                        dllUrl = (string)a["browser_download_url"];
                        dllSha256 = ParseSha256Digest((string)a["digest"]);
                    }
                }
            }
            string assetUrl = zipUrl ?? dllUrl;
            if(assetUrl == null) continue;
            if(best == null || v.CompareTo(best.Version) > 0) {
                best = new UpdateInfo {
                    Tag = tag,
                    Name = ParseReleaseName((string)rel["name"], tag),
                    Version = v,
                    Url = (string)rel["html_url"],
                    AssetUrl = assetUrl,
                    AssetIsZip = zipUrl != null,
                    AssetSha256 = zipUrl != null ? zipSha256 : dllSha256,
                };
            }
        }
        return best;
    }
    private static string ParseReleaseName(string title, string tag) {
        if(string.IsNullOrWhiteSpace(title)) return null;
        string name = title.Trim();
        if(!string.IsNullOrEmpty(tag) && name.StartsWith(tag, System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring(tag.Length);
        name = name.TrimStart(' ', '\t', '—', '–', '-', ':', '|');
        name = name.Trim();
        return name.Length == 0 || string.Equals(name, tag, System.StringComparison.OrdinalIgnoreCase)
            ? null
            : name;
    }
    private static string ParseSha256Digest(string digest) {
        const string prefix = "sha256:";
        if(string.IsNullOrEmpty(digest) || !digest.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return null;
        string hex = digest.Substring(prefix.Length);
        return hex.Length == 64 ? hex.ToLowerInvariant() : null;
    }
    private static string HashFileSha256(string path) {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] hash = sha.ComputeHash(stream);
        return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    public static async void Install(UpdateInfo info) {
        if(!MainCore.Host.SupportsSelfUpdate) return;
        if(info == null || Status == UpdateStatus.Installing) return;
        if(info.AssetUrl == null) {
            lastPercent = -1;
            Progress = 0f;
            Set(UpdateStatus.Installing);
            await SimulateDownload();
            Available = null;
            installedVersion = info.Version;
            Set(UpdateStatus.Installed, "DEV: simulated install — no files changed.");
            return;
        }
        lastPercent = -1;
        Progress = 0f;
        Set(UpdateStatus.Installing);
        try {
            await Task.Run(() => Download(info));
            Available = null;
            installedVersion = info.Version;
            Set(UpdateStatus.Installed);
        } catch(System.Exception ex) {
            Fail(UpdateFailure.InstallError, ex.Message);
            MainCore.Log.Wrn($"[Update] install failed: {ex.Message}");
        }
    }
    public static async void InstallLegacyRename(string legacyDllPath) {
        if(Status == UpdateStatus.Installing) return;
        lastPercent = -1;
        Progress = 0f;
        Set(UpdateStatus.Installing);
        try {
            UpdateInfo info = await Task.Run(() => FetchLatest(forceLatest: true));
            if(info == null) throw new System.Exception("no installable Quartz release found");
            await Task.Run(() => Download(info));
            RetireLegacyDll(legacyDllPath);
            Available = null;
            installedVersion = info.Version;
            Set(UpdateStatus.Installed);
            MainCore.Log.Msg($"[Update] migrated Koren.dll install to Quartz {info.Tag} — restart to finish");
        } catch(System.Exception ex) {
            Fail(UpdateFailure.InstallError, ex.Message);
            MainCore.Log.Wrn($"[Update] legacy rename install failed: {ex.Message}");
        }
    }
    private static async Task Download(UpdateInfo info) {
        string staging = Path.Combine(MainCore.Paths.TempPath, "Update");
        if(Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        if(info.AssetIsZip) {
            string stagedZip = Path.Combine(staging, "Quartz.zip");
            await DownloadFile(info.AssetUrl, stagedZip, 0f, 1f);
            VerifyChecksum(stagedZip, info);
            ExtractOverInstall(stagedZip);
        } else {
            string stagedQuartz = Path.Combine(staging, "Quartz.dll");
            await DownloadFile(info.AssetUrl, stagedQuartz, 0f, 1f);
            VerifyChecksum(stagedQuartz, info);
            ReplaceFile(stagedQuartz, Path.Combine(MainCore.Host.ModsPath, "Quartz.dll"));
        }
        DeleteIfExists(Path.Combine(MainCore.Host.ModsPath, "Quartz.Loader.ML.dll"));
        DeleteIfExists(Path.Combine(MainCore.Host.UserLibsPath, "Quartz.dll"));
    }
    private static void VerifyChecksum(string path, UpdateInfo info) {
        if(string.IsNullOrEmpty(info.AssetSha256)) {
            MainCore.Log.Wrn($"[Update] no checksum available for {info.Tag} — integrity not verified");
            return;
        }
        string actual = HashFileSha256(path);
        if(!string.Equals(actual, info.AssetSha256, System.StringComparison.OrdinalIgnoreCase)) {
            throw new System.Exception(
                $"checksum mismatch for {info.Tag}: expected {info.AssetSha256}, got {actual}");
        }
    }
    private static void ExtractOverInstall(string zipPath) {
        string gameRoot = MainCore.Host.UpdateExtractRoot;
        if(string.IsNullOrEmpty(gameRoot)) throw new System.Exception("couldn't resolve update extract root");
        string rootFull = Path.GetFullPath(gameRoot);
        string rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        foreach(ZipArchiveEntry entry in archive.Entries) {
            if(string.IsNullOrEmpty(entry.Name)) continue;
            string dest = Path.GetFullPath(Path.Combine(gameRoot, entry.FullName));
            if(!dest.StartsWith(rootPrefix, System.StringComparison.Ordinal)) {
                MainCore.Log.Wrn($"[Update] skipped suspicious zip entry: {entry.FullName}");
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string tmp = dest + ".krnew";
            try {
                if(File.Exists(tmp)) File.Delete(tmp);
            } catch {
            }
            entry.ExtractToFile(tmp, true);
            ReplaceFile(tmp, dest);
        }
    }
    private static void ReplaceFile(string src, string dest) {
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        if(File.Exists(dest)) {
            try {
                File.Delete(dest);
            } catch {
                string old = dest + ".old";
                try {
                    if(File.Exists(old)) File.Delete(old);
                } catch {
                }
                File.Move(dest, old);
            }
        }
        File.Move(src, dest);
    }
    private static void RetireLegacyDll(string path) {
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try {
            File.Delete(path);
        } catch {
            string old = path + ".old";
            try {
                if(File.Exists(old)) File.Delete(old);
            } catch {
            }
            try {
                File.Move(path, old);
            } catch(System.Exception ex) {
                MainCore.Log.Wrn($"[Update] couldn't retire {path}: {ex.Message}");
            }
        }
    }
    private static void DeleteIfExists(string path) {
        try {
            if(File.Exists(path)) File.Delete(path);
        } catch(System.Exception ex) {
            MainCore.Log.Wrn($"[Update] couldn't remove stale file {path}: {ex.Message}");
        }
    }
    private static async Task SimulateDownload() {
        System.Random rng = new();
        float p = 0f;
        while(p < 1f) {
            await Task.Delay(rng.Next(40, 140));
            p = System.Math.Min(1f, p + ((float)rng.NextDouble() * 0.05f) + 0.01f);
            ReportProgress(p);
        }
    }
    private static int lastPercent = -1;
    private static async Task DownloadFile(string url, string path, float from, float to) {
        using HttpResponseMessage resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        using Stream src = await resp.Content.ReadAsStreamAsync();
        using FileStream dst = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[64 * 1024];
        long done = 0;
        int n;
        while((n = await src.ReadAsync(buffer, 0, buffer.Length)) > 0) {
            dst.Write(buffer, 0, n);
            done += n;
            if(total > 0) ReportProgress(from + ((to - from) * done / total));
        }
    }
    private static void ReportProgress(float value) {
        int percent = (int)(value * 100f);
        if(percent == lastPercent) return;
        lastPercent = percent;
        Progress = value;
        MainThread.Enqueue(() => OnChanged?.Invoke());
    }
    public static void Skip(UpdateInfo info) {
        if(info == null) return;
        MainCore.Conf.SkippedVersion = info.Tag;
        MainCore.ConfMgr.RequestSave();
        lastSkipped = info;
        Available = null;
        Set(UpdateStatus.Skipped);
    }
    public static void UndoSkip() {
        MainCore.Conf.SkippedVersion = "";
        MainCore.ConfMgr.RequestSave();
        if(lastSkipped != null) {
            Available = lastSkipped;
            lastSkipped = null;
            Set(UpdateStatus.Available);
        } else {
            Check();
        }
    }
    private static UpdateInfo Simulated() => new() {
        Tag = "v" + Info.DisplayVersion,
        Name = "Simulated Release",
        Version = Info.Current,
        Url = Info.GithubLink,
        AssetUrl = null,
    };
    public static void SetDevSimulate(bool on) {
        DevSimulate = on;
        if(on) {
            Available = Simulated();
            Set(UpdateStatus.Available);
        } else {
            Available = null;
            Set(UpdateStatus.Idle);
        }
    }
}
