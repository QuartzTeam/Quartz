using System.Diagnostics;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using SkyHook;
using UnityEngine;
namespace Quartz.Features.ChatterBlocker;
public static class ChatterBlocker {
    public static SettingsFile<ChatterBlockerSettings> ConfMgr { get; private set; }
    public static ChatterBlockerSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<ChatterBlockerSettings>.Loaded("ChatterBlocker.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool IsActive() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf.Enabled;
    }
    private static bool HasAnyFilter() =>
        IsActive() || KeyLimiter.KeyLimiter.IsEnabled() || KeyLimiter.KeyLimiter.IsMenuBlockEnabled()
        || AutoDeafen.AutoDeafen.InjectGuardActive;
    private static long ThresholdMs() => Math.Max(0L, (long)Math.Round(Conf?.ThresholdMs ?? 0f));
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static long NowMs() => clock.ElapsedMilliseconds;
    private static readonly Dictionary<KeyCode, long> lastKeyPress = [];
    private static readonly Dictionary<ushort, long> lastAsyncKeyPress = [];
    private static readonly HashSet<KeyCode> reportedKeysThisFrame = [];
    private static readonly HashSet<KeyCode> injectedKeyHeldPrev = [];
    private static readonly bool DebugLog = false;
    private static bool AcceptNormalKey(KeyCode key, long now, long thresholdMs, bool active) {
        if(!active) return true;
        if(!lastKeyPress.TryGetValue(key, out long last)) {
            lastKeyPress[key] = now;
            return true;
        }
        long elapsed = now - last;
        if(elapsed > thresholdMs || elapsed <= 5L) {
            lastKeyPress[key] = now;
            return true;
        }
        if(DebugLog) MainCore.Log.Msg($"[ChatterBlocker] Blocked Key: {key} time: {elapsed}ms.");
        return false;
    }
    private static bool AcceptAsyncKey(ushort key, long now, long thresholdMs, bool active) {
        if(!active) return true;
        if(!lastAsyncKeyPress.TryGetValue(key, out long last)) {
            lastAsyncKeyPress[key] = now;
            return true;
        }
        long elapsed = now - last;
        if(elapsed > thresholdMs || elapsed <= 5L) {
            lastAsyncKeyPress[key] = now;
            return true;
        }
        if(DebugLog) MainCore.Log.Msg($"[ChatterBlocker] Blocked Async Key: {key} time: {elapsed}ms.");
        return false;
    }
    private static void RecordKeyStats(scrController controller, object key) {
        try {
            scrPlayer player = controller != null ? controller.playerOne : null;
            if(player == null || player.keyFrequency == null) return;
            player.keyFrequency[key] = player.keyFrequency.ContainsKey(key)
                ? player.keyFrequency[key] + 1
                : 1; 
            player.keyTotal++;
        } catch {
        }
    }
    private static void ResetKeyLimiterOverCounter(scrController controller) {
        if(controller != null && controller.playerOne != null) controller.playerOne.keyLimiterOverCounter = 0;
    }
    private static int CountValidKeysPressed() {
        scrController controller = scrController.instance;
        if(controller == null) return 0;
        ResetKeyLimiterOverCounter(controller);
        // Same gate as the RDInput blocks in KeyLimiterPatches — while the Quartz menu is
        // open, no hits register at all (mouse included), same as those other reads.
        if(KeyLimiter.KeyLimiter.IsMenuBlockActive()) return 0;
        bool chatterActive = IsActive();
        long now = NowMs();
        long threshold = ThresholdMs();
        int count = 0;
        reportedKeysThisFrame.Clear();
        foreach(AnyKeyCode mainPressKey in RDInput.GetMainPressKeys()) {
            object value = mainPressKey.value;
            if(value is KeyCode key) {
                KeyCode normalized = KeyLimiter.KeyLimiter.NormalizeKey(key);
                reportedKeysThisFrame.Add(normalized);
                if(AutoDeafen.AutoDeafen.IsInjectedKey(normalized)) continue;
                if(KeyLimiter.KeyLimiter.ShouldBlockKey(key)) continue;
                RecordKeyStats(controller, key);
                if(AcceptNormalKey(key, now, threshold, chatterActive)) count++;
            } else if(value is AsyncKeyCode asyncKey) {
                KeyCode physical = KeyLimiter.KeyLimiter.NormalizeKey(
                    KeyLimiter.KeyLimiter.HookKeyToPhysicalUnityKey(asyncKey.key, asyncKey.label));
                if(physical != KeyCode.None) reportedKeysThisFrame.Add(physical);
                if(AutoDeafen.AutoDeafen.IsInjectedKey(physical)) continue;
                if(KeyLimiter.KeyLimiter.ShouldBlockAsyncKeyFromHook(asyncKey.key, asyncKey.label)) continue;
                RecordKeyStats(controller, asyncKey);
                if(AcceptAsyncKey(asyncKey.key, now, threshold, chatterActive)) count++;
            }
        }
        count += CountKeysMissedByGame(controller, now, threshold, chatterActive);
        return count;
    }
    // scrPlayer.HitAutoFloors calls CountValidKeysPressed twice per batch (once through
    // ValidInputWasTriggered, once for the hit count), and async input runs one batch per
    // event tick plus a final per-frame batch. Injected keys are invisible to the game's
    // own masks, so their press edge must be computed once per frame and then reported to
    // every call of a single batch — consuming the edge on the first call starves the hit
    // count, while reporting it to every batch multiplies one press into several hits.
    // ValidInputWasTriggered itself early-returns unless Input.anyKeyDown or a game-mask
    // key is down, so on quiet frames (e.g. the frame an injected key is RELEASED) the
    // game never calls CountValidKeysPressed and ComputeInjectedKeys cannot observe the
    // release — injectedKeyHeldPrev would keep the key and swallow the next press of the
    // same key (the consecutive-Tab bug). SampleInjectedKeyReleases, driven every frame
    // by KeyLimiter's Ticker, clears release edges independently of game calls.
    private static int injectionBatch;
    private static bool inPlayerBatch;
    private static int injectedComputeFrame = -1;
    private static int injectedBatch = -1;
    private static int injectedCount;
    public static void NotePlayerBatch(bool entered) {
        inPlayerBatch = entered;
        if(entered) injectionBatch++;
    }
    private static int CountKeysMissedByGame(scrController controller, long now, long threshold, bool chatterActive) {
        if(!KeyLimiter.KeyLimiter.IsActive() || !KeyLimiter.KeyLimiter.InPlayerControl()) {
            injectedKeyHeldPrev.Clear();
            injectedComputeFrame = -1;
            return 0;
        }
        if(!inPlayerBatch) return 0;
        int frame = UnityEngine.Time.frameCount;
        if(injectedComputeFrame != frame) {
            injectedComputeFrame = frame;
            injectedBatch = injectionBatch;
            injectedCount = ComputeInjectedKeys(controller, now, threshold, chatterActive);
        }
        return injectionBatch == injectedBatch ? injectedCount : 0;
    }
    private static int ComputeInjectedKeys(scrController controller, long now, long threshold, bool chatterActive) {
        int[] allowed = KeyLimiter.KeyLimiter.Conf?.AllowedKeys;
        if(allowed == null || allowed.Length == 0) {
            injectedKeyHeldPrev.Clear();
            return 0;
        }
        bool asyncActive = AsyncKeyboardActive();
        int injected = 0;
        for(int i = 0; i < allowed.Length; i++) {
            KeyCode key = KeyLimiter.KeyLimiter.NormalizeKey((KeyCode)allowed[i]);
            if(key == KeyCode.None || KeyLimiter.KeyLimiter.IsMouseKey(key)) continue;
            if(reportedKeysThisFrame.Contains(key)) {
                injectedKeyHeldPrev.Add(key);
                continue;
            }
            // When the async keyboard is live it reports every KeyLabel (AllAsyncKeys),
            // so any key the hook has ever seen — hook-only modifiers included — is already
            // counted by the game-visible async path above. Injecting it too double-counts it;
            // the reportedKeysThisFrame guard can't catch that because the async main-loop add
            // and this injected pass land in different per-tick / per-frame batches. Hook-only
            // modifiers must NOT be excluded here (that was the RShift/RControl double-count).
            if(asyncActive && KeyLimiter.KeyLimiter.HookEverSaw(key)) {
                injectedKeyHeldPrev.Remove(key);
                continue;
            }
            bool held;
            try { held = UnityEngine.Input.GetKey(key); }
            catch { continue; }
            if(!held) held = KeyLimiter.KeyLimiter.HookKeyHeld(key);
            if(held && !injectedKeyHeldPrev.Contains(key)) {
                RecordKeyStats(controller, key);
                if(AcceptNormalKey(key, now, threshold, chatterActive)) injected++;
            }
            if(held) injectedKeyHeldPrev.Add(key);
            else injectedKeyHeldPrev.Remove(key);
        }
        return injected;
    }
    // Removal only: press edges stay exclusively in ComputeInjectedKeys (inside a player
    // batch), preserving the single-count-per-press guarantee above. Must consult
    // HookKeyHeld too, or a held hook-only key (mac modifiers) would be cleared every
    // frame and re-edge into phantom repeat hits.
    private static readonly List<KeyCode> injectedReleaseScratch = [];
    public static void SampleInjectedKeyReleases() {
        if(injectedKeyHeldPrev.Count == 0) return;
        injectedReleaseScratch.Clear();
        foreach(KeyCode key in injectedKeyHeldPrev) injectedReleaseScratch.Add(key);
        for(int i = 0; i < injectedReleaseScratch.Count; i++) {
            KeyCode key = injectedReleaseScratch[i];
            bool held;
            try { held = UnityEngine.Input.GetKey(key); }
            catch { held = false; }
            if(!held) held = KeyLimiter.KeyLimiter.HookKeyHeld(key);
            if(!held) injectedKeyHeldPrev.Remove(key);
        }
    }
    private static bool AsyncKeyboardActive() {
        try {
            return RDInput.asyncKeyboard != null && RDInput.asyncKeyboard.isActive;
        } catch {
            return false;
        }
    }
    [HarmonyPatch(typeof(scrPlayer), "Simulated_PlayerControl_Update")]
    private static class SimulatedPlayerControlUpdatePatch {
        private static void Prefix() => NotePlayerBatch(true);
        private static void Postfix() => NotePlayerBatch(false);
    }
    [HarmonyPatch(typeof(scrPlayer), "CountValidKeysPressed")]
    private static class CountValidKeysPressedPatch {
        private static bool Prefix(ref int __result) {
            if(!HasAnyFilter()) return true;
            __result = CountValidKeysPressed();
            return false;
        }
    }
    [HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
    private static class HookCallbackPatch {
        private static void Prefix(SkyHookEvent __0) {
            try {
                SkyHookEvent ev = __0;
                if(KeyLimiter.KeyLimiter.IsMouseLabel(ev.Label)) return;
                KeyLimiter.KeyLimiter.NoteHookEvent(
                    KeyLimiter.KeyLimiter.HookKeyToPhysicalUnityKey(ev.Key, ev.Label),
                    ev.Type == SkyHook.EventType.KeyPressed);
            } catch {
            }
        }
    }
}
