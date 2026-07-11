using HarmonyLib;
using Quartz.Async;
using Quartz.Core;
using Quartz.Features.Status;
using Quartz.IO;
using MonsterLove.StateMachine;
using UnityEngine;
namespace Quartz.Features.AutoDeafen;
public static class AutoDeafen {
    public static SettingsFile<AutoDeafenSettings> ConfMgr { get; private set; }
    public static AutoDeafenSettings Conf => ConfMgr?.Data;
    private static DiscordRpc rpc;
    private static string configClientId;
    private static string configToken;
    private static bool desiredDeaf;
    private static string status = "off";
    private static bool suppressUntilRestart;
    private static bool runStartCaptured;
    private static bool startedFromFirstTile;
    private const string TutorialUrl = "https://www.youtube.com/watch?v=1q4gB0ArypQ";
    private const int MaxTokenLength = 4096;
    private static string TokenPath => Path.Combine(MainCore.Paths.RootPath, "DiscordAccessToken.secret");
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<AutoDeafenSettings>.Loaded("AutoDeafen.json");
        LoadAccessToken();
        EnsureTicker();
    }
    public static void Save() => ConfMgr?.RequestSave();
    public static bool ShortcutSupported =>
        Application.platform == RuntimePlatform.WindowsPlayer
        || Application.platform == RuntimePlatform.WindowsEditor;
    public static string EffectiveMode =>
        ShortcutSupported && Conf != null && Conf.IsShortcut
            ? AutoDeafenSettings.ModeShortcut
            : AutoDeafenSettings.ModeBot;
    public static string Status {
        get {
            if(EffectiveMode == AutoDeafenSettings.ModeShortcut) {
                string shortcut = "shortcut " + ChordText();
                return desiredDeaf ? shortcut + " / deaf" : shortcut;
            }
            string rpcStatus = rpc != null ? rpc.Status : status;
            string oauthStatus = DiscordOAuthServer.Status;
            if(!string.IsNullOrEmpty(Trim(Conf?.DiscordAccessToken)) && !DiscordOAuthServer.Running) oauthStatus = "authorized";
            string combined = oauthStatus + " / " + rpcStatus;
            return desiredDeaf ? combined + " / deaf" : combined;
        }
    }
    public static string ChordText() {
        if(Conf == null) return "";
        System.Text.StringBuilder sb = new();
        if(Conf.ShortcutCtrl) sb.Append("Ctrl+");
        if(Conf.ShortcutShift) sb.Append("Shift+");
        if(Conf.ShortcutAlt) sb.Append("Alt+");
        if(Conf.ShortcutMeta) sb.Append(Keybind.IsMac ? "Cmd+" : "Win+");
        sb.Append(Keybind.KeyName((KeyCode)Conf.ShortcutKey));
        return sb.ToString();
    }
    private static void Tick(float progress01) {
        EnsureConf();
        if(!MainCore.IsModEnabled || !Conf.Enabled) {
            Stop();
            status = "off";
            return;
        }
        Conf.DeafenAtPercent = Mathf.Clamp(Conf.DeafenAtPercent, 0f, 100f);
        if(EffectiveMode == AutoDeafenSettings.ModeShortcut) {
            if(rpc != null) StopRpc();
            if(DiscordOAuthServer.Running) DiscordOAuthServer.Stop();
        } else {
            if(string.IsNullOrWhiteSpace(Conf.DiscordAccessToken)) {
                StopRpc();
                status = "waiting for authorization";
                return;
            }
            string clientId = DiscordOAuthServer.ClientId;
            string token = Trim(Conf.DiscordAccessToken);
            if(rpc == null
               || !string.Equals(configClientId, clientId, StringComparison.Ordinal)
               || !string.Equals(configToken, token, StringComparison.Ordinal)) {
                Restart(clientId, token);
            }
        }
        if(progress01 >= 0f && !runStartCaptured) {
            startedFromFirstTile = ProgressTracker.IsFirstTileRunStart();
            runStartCaptured = true;
        }
        bool eligibleStart = !Conf.OnlyFromStart || (runStartCaptured && startedFromFirstTile);
        bool shouldDeaf = !suppressUntilRestart
            && progress01 >= 0f
            && InRealPlay()
            && eligibleStart
            && Mathf.Clamp01(progress01) * 100f >= Conf.DeafenAtPercent;
        if(shouldDeaf != desiredDeaf) {
            desiredDeaf = shouldDeaf;
            ApplyDeaf(shouldDeaf);
            MainCore.Log.Msg("[AutoDeafen] desired deaf = " + shouldDeaf);
        }
    }
    private static void ApplyDeaf(bool deaf) {
        if(EffectiveMode == AutoDeafenSettings.ModeShortcut) {
            NativeKeySender.SendChord(
                Conf.ShortcutCtrl, Conf.ShortcutShift, Conf.ShortcutAlt, Conf.ShortcutMeta,
                (KeyCode)Conf.ShortcutKey
            );
        } else {
            rpc?.SetDeaf(deaf);
        }
    }
    private const int InjectBypassWindowMs = 150;
    private const int InjectGuardWindowMs = 400;
    private static volatile int injectBypassUntil;
    private static volatile int injectGuardUntil;
    private static readonly HashSet<KeyCode> injectedKeys = [];
    public static bool InjectBypassActive =>
        injectBypassUntil != 0 && Environment.TickCount - injectBypassUntil <= 0;
    public static bool InjectGuardActive =>
        injectGuardUntil != 0 && Environment.TickCount - injectGuardUntil <= 0;
    public static bool IsInjectedKey(KeyCode key) =>
        InjectGuardActive && injectedKeys.Contains(key);
    internal static void MarkInject(IEnumerable<KeyCode> normalizedKeys) {
        injectedKeys.Clear();
        foreach(KeyCode k in normalizedKeys) injectedKeys.Add(k);
        int now = Environment.TickCount;
        injectBypassUntil = now + InjectBypassWindowMs;
        injectGuardUntil = now + InjectGuardWindowMs;
    }
    private static void OnRunReset() {
        suppressUntilRestart = false;
        runStartCaptured = false;
        Undeafen();
    }
    private static void OnRunEnded() {
        suppressUntilRestart = true;
        Undeafen();
    }
    private static void OnRunHide() => Undeafen();
    private static void Undeafen() {
        if(!desiredDeaf) return;
        desiredDeaf = false;
        try { ApplyDeaf(false); } catch { }
    }
    public static void Stop() {
        Undeafen();
        if(rpc != null) StopRpc();
        DiscordOAuthServer.Stop();
        desiredDeaf = false;
        configClientId = null;
        configToken = null;
        suppressUntilRestart = false;
        runStartCaptured = false;
    }
    public static void OpenAuthorizeUrl() {
        EnsureConf();
        DiscordOAuthServer.OpenAuthorizeUrl();
    }
    public static void OpenTutorial() => DiscordOAuthServer.OpenUrl(TutorialUrl);
    public static string AuthorizeUrl() {
        EnsureConf();
        return DiscordOAuthServer.AuthorizeUrl();
    }
    public static void CopyAuthorizeUrl() {
        EnsureConf();
        DiscordOAuthServer.CopyAuthorizeUrl();
    }
    public static void Unlink() {
        Stop();
        EnsureConf();
        Conf.DiscordAccessToken = "";
        try {
            if(File.Exists(TokenPath)) File.Delete(TokenPath);
            status = "unlinked";
        } catch(Exception e) {
            status = "unlink failed";
            MainCore.Log.Err("[AutoDeafen] couldn't remove stored Discord token: " + e.Message);
        }
    }
    private static bool InRealPlay() {
        try { return scnGame.instance != null; }
        catch { return false; }
    }
    private static void Restart(string clientId, string token) {
        StopRpc();
        configClientId = clientId;
        configToken = token;
        status = "starting";
        rpc = new DiscordRpc(clientId, token);
        rpc.Start();
    }
    private static void StopRpc() {
        if(rpc == null) return;
        try { rpc.SetDeaf(false); } catch { }
        try { rpc.Stop(); } catch { }
        rpc = null;
        desiredDeaf = false;
        configClientId = null;
        configToken = null;
    }
    internal static void SaveAccessToken(string token) {
        token = Trim(token);
        if(token.Length == 0 || token.Length > MaxTokenLength || Conf == null) return;
        MainThread.Enqueue(() => {
            if(Conf == null || string.Equals(Conf.DiscordAccessToken, token, StringComparison.Ordinal)) return;
            try {
                AtomicFile.WriteAllText(TokenPath, token);
                Conf.DiscordAccessToken = token;
            } catch(Exception e) {
                MainCore.Log.Err("[AutoDeafen] couldn't store Discord token: " + e.Message);
            }
        });
    }
    private static void LoadAccessToken() {
        string legacy = Trim(Conf?.LegacyDiscordAccessToken);
        bool migrated = false;
        try {
            string persisted = "";
            if(File.Exists(TokenPath)) {
                FileInfo info = new(TokenPath);
                if(info.Length > MaxTokenLength) throw new IOException("stored token exceeds size limit");
                persisted = Trim(File.ReadAllText(TokenPath));
            }
            if(persisted.Length > MaxTokenLength) throw new IOException("stored token exceeds size limit");
            if(persisted.Length > 0) {
                Conf.DiscordAccessToken = persisted;
            } else if(legacy.Length > 0 && legacy.Length <= MaxTokenLength) {
                AtomicFile.WriteAllText(TokenPath, legacy);
                Conf.DiscordAccessToken = legacy;
                migrated = true;
            }
            if(legacy.Length > 0 && (persisted.Length > 0 || migrated)) ConfMgr.Save();
        } catch(Exception e) {
            MainCore.Log.Err("[AutoDeafen] couldn't load stored Discord token: " + e.Message);
        } finally {
            Conf.LegacyDiscordAccessToken = "";
        }
    }
    private static string Trim(string value) => (value ?? "").Trim();
    private static Ticker ticker;
    private static void EnsureTicker() {
        if(ticker != null || MainCore.Root == null) return;
        ticker = MainCore.Root.AddComponent<Ticker>();
    }
    private sealed class Ticker : MonoBehaviour {
        private void Update() {
            float progress = GameStats.InGame ? Mathf.Clamp01(GameStats.Progress) : -1f;
            Tick(progress);
        }
    }
    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class RunStartPatch {
        private static void Postfix() {
            if(MainCore.IsModEnabled) OnRunReset();
        }
    }
    [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
    private static class RunEndPatch {
        private static void Postfix(Enum newState) {
            if(!MainCore.IsModEnabled || newState is not States state) return;
            if(state == States.Fail2 || state == States.Won) OnRunEnded();
        }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class RunHidePatch {
        private static void Postfix() {
            if(MainCore.IsModEnabled) OnRunHide();
        }
    }
}
