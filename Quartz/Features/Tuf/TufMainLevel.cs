using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Quartz.Compat.Game;
namespace Quartz.Features.Tuf;
internal static class TufMainLevel {
    private static bool returnToLevelSelectOnExit;
    private const string CuratorTag = "?curator_clanid=34150082";
    public enum TufMainAction { None, Play, BuyDlc }
    public static TufMainAction Resolve(TufLevel level, out string codeOrUrl) {
        codeOrUrl = "";
        if(!TryResolveCode(level, out string code)) return TufMainAction.None;
        string world = WorldOf(code);
        if(IsWorldAccessible(world)) {
            codeOrUrl = code;
            return TufMainAction.Play;
        }
        string url = DlcStoreUrl(world);
        if(!string.IsNullOrEmpty(url)) {
            codeOrUrl = url;
            return TufMainAction.BuyDlc;
        }
        return TufMainAction.None;
    }
    public static bool TryResolveCode(TufLevel level, out string code) {
        code = "";
        if(level == null || level.DownloadUri != null) return false;
        if(!level.MainLevelResolved) {
            level.MainLevelCode = Compute(level.Suffix);
            level.MainLevelResolved = true;
        }
        code = level.MainLevelCode;
        return code.Length > 0;
    }
    private static string Compute(string suffix) {
        if(string.IsNullOrWhiteSpace(suffix)) return "";
        string s = suffix.Trim();
        if(s.Length >= 2 && s[0] == '(' && s[^1] == ')') s = s[1..^1].Trim();
        if(s.Length == 0) return "";
        int dash = s.IndexOf('-');
        if(dash > 0 && dash < s.Length - 1) {
            string world = s[..dash].Trim();
            string lvl = s[(dash + 1)..].Trim();
            if(IsWorldCode(world) && IsRealLevel(world, lvl)) return world + "-" + lvl;
        }
        string joined = s.Replace("-", "").Trim();
        if(joined != s && IsWorldCode(joined)) return joined + "-X";
        return "";
    }
    private static string WorldOf(string code) {
        int dash = code.IndexOf('-');
        return dash > 0 ? code[..dash] : code;
    }
    private static string DlcStoreUrl(string world) {
        try {
            var managers = DLCManager.DLCManagers;
            if(managers != null)
                foreach(DLCManager mgr in managers)
                    if(mgr != null && mgr.IsDLCLevel(world))
                        return StoreUrlFor(mgr);
        } catch { }
        return null;
    }
    private static string StoreUrlFor(DLCManager mgr) {
        if(SteamInitialized() && mgr.steamAppId != 0)
            return $"https://store.steampowered.com/app/{mgr.steamAppId}/{CuratorTag}";
        return mgr.groupName switch {
            "Neo Cosmos" => "https://fizzd.itch.io/neo-cosmos",
            _ => null,
        };
    }
    public static void OpenStore(string url) {
        if(string.IsNullOrEmpty(url)) return;
        if(TryOpenInSteamOverlay(url)) return;
        try {
            Application.OpenURL(url);
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not open DLC store page: " + e);
        }
    }
    private static PropertyInfo steamInitializedProp;
    private static bool steamInitializedResolved;
    private static bool SteamInitialized() {
        try {
            if(!steamInitializedResolved) {
                Type manager = AccessTools.TypeByName("SteamManager");
                steamInitializedProp = manager != null ? AccessTools.Property(manager, "Initialized") : null;
                steamInitializedResolved = true;
            }
            return steamInitializedProp?.GetValue(null) is true;
        } catch {
            return false;
        }
    }
    private static bool TryOpenInSteamOverlay(string url) {
        try {
            if(ADOBase.isSwitch || !SteamInitialized()) return false;
            Type friends = AccessTools.TypeByName("Steamworks.SteamFriends");
            MethodInfo open = friends != null ? AccessTools.Method(friends, "ActivateGameOverlayToWebPage") : null;
            if(open == null) return false;
            ParameterInfo[] ps = open.GetParameters();
            object[] args = ps.Length >= 2
                ? new object[] { url, Enum.ToObject(ps[1].ParameterType, 0) }
                : new object[] { url };
            open.Invoke(null, args);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] Steam overlay open failed, using browser: " + e);
            return false;
        }
    }
    private static bool IsWorldAccessible(string world) {
        try {
            var managers = DLCManager.DLCManagers;
            if(managers != null) {
                foreach(DLCManager mgr in managers)
                    if(mgr != null && mgr.IsDLCLevel(world))
                        return mgr.installed && mgr.upToDate;
                return true;
            }
        } catch { }
        return !LooksLikeDlcWorld(world);
    }
    private static bool LooksLikeDlcWorld(string world) {
        if(string.IsNullOrEmpty(world)) return false;
        if(world[0] == 'T') return true;
        return world.EndsWith("EX");
    }
    private static bool IsWorldCode(string world) {
        if(world.Length == 0) return false;
        foreach(char c in world)
            if(!char.IsLetterOrDigit(c)) return false;
        try { return GameApi.WorldTable()?.Contains(world) == true; }
        catch { return false; }
    }
    private static bool IsRealLevel(string world, string lvl) {
        try {
            int levelCount = GameApi.WorldLevelCount(world);
            if(levelCount < 0) return false;
            if(lvl == "X") return true;
            return int.TryParse(lvl, out int n) && n >= 1 && n <= levelCount;
        } catch {
            return false;
        }
    }
    public static bool Launch(string code) {
        if(string.IsNullOrEmpty(code)) return false;
        try {
            returnToLevelSelectOnExit = true;
            scrController controller = ADOBase.controller;
            if(controller != null) {
                controller.EnterLevel(code, false);
                return true;
            }
            bool internalLevel = GameApi.IsInternalLevelCode(code);
            GCS.speedTrialMode = false;
            GCS.nextSpeedRun = 1f;
            GCS.practiceMode = false;
            GCS.customLevelPaths = null;
            GCS.customLevelIndex = 0;
            GCS.internalLevelName = internalLevel ? code : null;
            GCS.sceneToLoad = internalLevel ? "scnGame" : code;
            if(!GameApi.LoadSceneWithTransition(WipeDirection.StartsFromRight))
                SceneManager.LoadScene(GCS.sceneToLoad);
            return true;
        } catch(Exception e) {
            returnToLevelSelectOnExit = false;
            MainCore.Log.Wrn($"[TUF] base-game level launch failed for '{code}': {e}");
            return false;
        }
    }
    [HarmonyPatch(typeof(scrController), "QuitToMainMenu")]
    private static class ExitToHubPatch {
        private static void Postfix() {
            if(!returnToLevelSelectOnExit) return;
            returnToLevelSelectOnExit = false;
            try {
                if(!GCS.webVersion && GCS.customLevelPaths == null)
                    GCS.sceneToLoad = GameApi.SceneLevelSelect;
            } catch(Exception e) {
                MainCore.Log.Wrn("[TUF] exit-to-hub redirect failed: " + e);
            }
        }
    }
}
