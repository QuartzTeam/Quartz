using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
namespace Quartz.IO;
public static partial class ProfileManager {
    public const string DEFAULT_NAME = "Default";
    public const string EXPORT_EXTENSION = "qprofile";
    public const string LEGACY_EXTENSION = "krprofile";
    public static readonly string[] ImportExtensions = [EXPORT_EXTENSION, LEGACY_EXTENSION];
    private const string BUNDLE_TYPE = "QuartzProfile";
    private static readonly HashSet<string> bundleTypes = new(StringComparer.Ordinal) {
        BUNDLE_TYPE,
        "KorenProfile",
    };
    private static bool IsProfileBundle(JToken bundle) =>
        bundle?["Type"]?.Value<string>() is string type && bundleTypes.Contains(type);
    private static readonly HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase) {
        "PlayCount.json",
        "Profiles.json",
    };
    // A preset ships one author's look to everyone, so it must not carry settings that
    // describe that author rather than the look. ProfileBundle.StripPresetImposed drops
    // these fields from the config file when a bundle is imported as a preset.
    private static readonly string[] presetImposed = [nameof(CoreSettings.Language)];
    public static string Active { get; private set; } = DEFAULT_NAME;
    public static string ProfilesPath => Path.Combine(MainCore.Paths.RootPath, "Profiles");
    private static string PointerPath => Path.Combine(MainCore.Paths.RootPath, "Profiles.json");
    private static string SwitchMarkerPath => Path.Combine(ProfilesPath, ".switch.json");
    private static string SwitchRollbackPath => Path.Combine(ProfilesPath, ".switch-rollback");
    private static string DirOf(string name) => Path.Combine(ProfilesPath, name);
    public static void Initialize() {
        try {
            Directory.CreateDirectory(ProfilesPath);
            RecoverProfileDirectories();
            RecoverInterruptedSwitch();
            if(File.Exists(PointerPath)) {
                JToken token = JToken.Parse(File.ReadAllText(PointerPath));
                string name = token["Active"]?.Value<string>();
                if(!string.IsNullOrWhiteSpace(name) && Directory.Exists(DirOf(name))) Active = name;
            }
            if(!Directory.Exists(DirOf(Active))) {
                Active = DEFAULT_NAME;
                CaptureTo(Active);
                SavePointer();
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Initialize failed: {e}");
        }
    }
    public static string[] List() {
        try {
            return [..
                Directory.GetDirectories(ProfilesPath)
                    .Where(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            ];
        } catch {
            return [Active];
        }
    }
    public static bool Exists(string name)
        => !string.IsNullOrEmpty(name) && Directory.Exists(DirOf(name));
    public static string Sanitize(string name) => ProfileNames.Sanitize(name);
    public static bool Create(string name) {
        name = Sanitize(name);
        if(name == null || Exists(name)) return false;
        try {
            if(!CaptureActive()) return false;
            CaptureTo(name);
            Active = name;
            SavePointer();
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Create '{name}' failed: {e}");
            return false;
        }
    }
    public static string CreateUnique(string name) {
        name = ProfileNames.Unique(name, Exists);
        return Create(name) ? name : null;
    }
    public static bool Delete(string name) {
        if(name == Active || !Exists(name)) return false;
        try {
            Directory.Delete(DirOf(name), true);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Delete '{name}' failed: {e}");
            return false;
        }
    }
    public static bool CaptureActive() {
        try {
            if(!SettingsRegistry.SaveAll()) return false;
            CaptureTo(Active);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Capture '{Active}' failed: {e}");
            return false;
        }
    }
    public static bool Apply(string name) {
        if(name == Active || !Exists(name)) return false;
        string previous = Active;
        Dictionary<string, string> previousFiles = null;
        bool runtimeStopped = false;
        bool switchStarted = false;
        try {
            if(!CaptureActive()) throw new IOException("could not capture the active profile before switching");
            Dictionary<string, string> targetFiles = ReadSettingsDirectory(DirOf(name), validateJson: true);
            previousFiles = ReadSettingsDirectory(MainCore.Paths.RootPath, validateJson: false);
            BeginSwitch(previous, previousFiles);
            switchStarted = true;
            SettingsRegistry.CancelPendingSaves();
            MainCore.Runtime.SetModEnabled(false, true);
            runtimeStopped = true;
            ReplaceLiveSettings(targetFiles);
            SettingsRegistry.ReloadAll();
            ApplyRuntimeSettings();
            Active = name;
            SavePointer();
            MainCore.Runtime.SetModEnabled(MainCore.Conf.Active, true);
            CompleteSwitch();
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Apply '{name}' failed: {e}");
            if(previousFiles != null) {
                try {
                    ReplaceLiveSettings(previousFiles);
                    SettingsRegistry.ReloadAll();
                    ApplyRuntimeSettings();
                    Active = previous;
                    SavePointer();
                    if(runtimeStopped) MainCore.Runtime.SetModEnabled(MainCore.Conf.Active, true);
                    if(switchStarted) CompleteSwitch();
                } catch(Exception rollbackError) {
                    MainCore.Log.Err($"[{nameof(ProfileManager)}] Rollback '{previous}' failed: {rollbackError}");
                }
            }
            return false;
        }
    }
    public static bool Export(string name, string destPath) {
        if(!Exists(name) || string.IsNullOrEmpty(destPath)) return false;
        try {
            if(name == Active && !CaptureActive()) return false;
            JObject files = [];
            foreach(string file in Directory.GetFiles(DirOf(name), "*.json")) {
                try {
                    files[Path.GetFileName(file)] = JToken.Parse(File.ReadAllText(file));
                } catch {
                }
            }
            JObject bundle = new() {
                ["Type"] = BUNDLE_TYPE,
                ["Version"] = Info.Version,
                ["Name"] = name,
                ["Files"] = files,
            };
            AtomicFile.WriteAllText(destPath, bundle.ToString());
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Export '{name}' failed: {e}");
            return false;
        }
    }
    private static string ConfigFileName => Path.GetFileName(MainCore.Paths.ConfigPath);
    public static string Import(string srcPath, bool asPreset = false) {
        try {
            JToken bundle = JToken.Parse(File.ReadAllText(srcPath));
            if(!IsProfileBundle(bundle) || bundle["Files"] is not JObject files) {
                return null;
            }
            string name = Sanitize(bundle["Name"]?.Value<string>())
                ?? Sanitize(Path.GetFileNameWithoutExtension(srcPath))
                ?? "Imported";
            name = Uniquify(name);
            WriteProfileDirectory(
                DirOf(name),
                ProfileBundle.ReadFiles(files, excluded, asPreset, ConfigFileName, presetImposed)
            );
            return name;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Import '{srcPath}' failed: {e}");
            return null;
        }
    }
}
