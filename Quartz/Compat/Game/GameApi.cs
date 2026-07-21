using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Compat.Game;
public static class GameApi {
    private static readonly Type MarginTrackerType = Refl.Type("scrMarginTracker");
    private static readonly Type MistakesManagerType = Refl.Type("scrMistakesManager");
    private static readonly Type PlayerManagerType = Refl.Type("scrPlayerManager");
    private static readonly Refl.Member CtrlMistakes = new(typeof(scrController), "mistakesManager");
    private static readonly Refl.Member CtrlPlanetary = new(typeof(scrController), "planetarySystem");
    private static readonly Refl.Member CtrlPlayerOne = new(typeof(scrController), "playerOne");
    private static readonly Refl.Member AccPercent = new(MistakesManagerType, "percentAcc");
    private static readonly Refl.Member XAccPercent = new(MistakesManagerType, "percentXAcc");
    private static readonly Refl.Member PlayerCountMember = new(PlayerManagerType, "playerCount");
    private static readonly Refl.Member PlayersMember = new(PlayerManagerType, "players");
    private static readonly Refl.Member MarginTrackerMember =
        new(Refl.Type("scrPlayer"), "marginTracker");
    private static readonly Refl.Member PlayerManagerMember =
        new(typeof(ADOBase), "playerManager");
    public static MethodBase AddHitTarget =>
        Refl.Method(MarginTrackerType ?? MistakesManagerType, "AddHit", 1);
    public static scrMistakesManager MistakesManager {
        get {
            scrController ctrl = scrController.instance;
            return ctrl == null ? null : CtrlMistakes.Get(ctrl) as scrMistakesManager;
        }
    }
    public static float PercentAcc(scrMistakesManager m) =>
        m == null ? 1f : AccPercent.Get(m, 1f);
    public static float PercentXAcc(scrMistakesManager m) =>
        m == null ? 1f : XAccPercent.Get(m, 1f);
    public static int PlayerCount() {
        if(!PlayerCountMember.Exists) return 1;
        int n = PlayerCountMember.Get(null, 1);
        return n < 1 ? 1 : n;
    }
    public static object Tracker(int playerID) {
        if(MarginTrackerType == null)
            return playerID == 0 ? MistakesManager : null;
        object pm = PlayerManagerMember.Get(null);
        if(pm == null) return null;
        if(PlayersMember.Get(pm) is not Array players) return null;
        if(playerID < 0 || playerID >= players.Length) return null;
        object player = players.GetValue(playerID);
        return player == null ? null : MarginTrackerMember.Get(player);
    }
    private static readonly Type TrackerType = MarginTrackerType ?? MistakesManagerType;
    private static readonly MethodInfo TrackerGetDeaths = Refl.Method(TrackerType, "GetDeaths", 0);
    private static readonly MethodInfo TrackerGetHits = Refl.Method(TrackerType, "GetHits", 1);
    private static readonly Refl.Member TrackerCounts = new(TrackerType, "hitMarginsCount");
    private static readonly Refl.Member TrackerMargins = new(TrackerType, "hitMargins");
    private static readonly Refl.Member TrackerDeadTiles = new(TrackerType, "deadTiles");
    public static int GetDeaths(object tracker) =>
        tracker == null ? 0 : Refl.Invoke(TrackerGetDeaths, tracker) as int? ?? 0;
    public static int GetHits(object tracker, HitMargin hit) =>
        tracker == null ? 0 : Refl.Invoke(TrackerGetHits, tracker, hit) as int? ?? 0;
    public static int[] HitMarginCounts(object tracker) =>
        tracker == null ? null : TrackerCounts.Get(tracker) as int[];
    public static int HitMarginTotal(object tracker) =>
        tracker != null && TrackerMargins.Get(tracker) is System.Collections.ICollection c ? c.Count : -1;
    public static int DeadTiles(object tracker) =>
        tracker == null ? 0 : TrackerDeadTiles.Get(tracker, 0);
    public static PlanetarySystem Planetary(scrController ctrl) =>
        ctrl == null ? null : CtrlPlanetary.Get(ctrl) as PlanetarySystem;
    public static object PlayerOne() {
        scrController ctrl = scrController.instance;
        return ctrl == null ? null : CtrlPlayerOne.Get(ctrl);
    }
    private static object PlayerState(scrController ctrl) {
        if(ctrl == null) return null;
        return CtrlPlayerOne.Exists ? CtrlPlayerOne.Get(ctrl) : ctrl;
    }
    private static readonly Type PlayerStateType = Refl.Type("scrPlayer") ?? typeof(scrController);
    private static readonly Refl.Member KeyFrequencyMember = new(PlayerStateType, "keyFrequency");
    private static readonly Refl.Member KeyTotalMember = new(PlayerStateType, "keyTotal");
    private static readonly Refl.Member KeyLimiterOverMember = new(PlayerStateType, "keyLimiterOverCounter");
    public static void RecordKeyPress(scrController ctrl, object key) {
        object state = PlayerState(ctrl);
        if(state == null) return;
        if(KeyFrequencyMember.Get(state) is not IDictionary<object, int> freq) return;
        freq[key] = freq.TryGetValue(key, out int pressCount) ? pressCount + 1 : 1;
        KeyTotalMember.Set(state, KeyTotalMember.Get(state, 0) + 1);
    }
    public static void ResetKeyLimiterOverCounter(scrController ctrl) {
        object state = PlayerState(ctrl);
        if(state != null) KeyLimiterOverMember.Set(state, 0);
    }
    private static readonly MethodInfo FailByHitboxMethod =
        Refl.Method(PlayerStateType, "DieByHitbox", 1) ?? Refl.Method(PlayerStateType, "FailByHitbox", 1);
    public static bool FailByHitbox(scrController ctrl, string reason) {
        object state = PlayerState(ctrl);
        if(state == null || FailByHitboxMethod == null) return false;
        Refl.Invoke(FailByHitboxMethod, state, reason ?? "");
        return true;
    }
    private static readonly Refl.Member PlanetarySpeedMember = new(typeof(PlanetarySystem), "speed");
    private static readonly Refl.Member CtrlSpeedMember = new(typeof(scrController), "speed");
    public static double PlanetSpeed(scrController ctrl) {
        if(ctrl == null) return 1.0;
        if(PlanetarySpeedMember.Exists && Planetary(ctrl) is PlanetarySystem ps)
            return PlanetarySpeedMember.Get(ps, 1.0);
        return CtrlSpeedMember.Get(ctrl, 1.0);
    }
    public static void SetPlanetSpeed(double speed) {
        scrController ctrl = scrController.instance;
        if(ctrl == null) return;
        if(!PlanetarySpeedMember.Exists) {
            CtrlSpeedMember.Set(ctrl, speed);
            return;
        }
        foreach(PlanetarySystem sys in AllPlanetarySystems()) PlanetarySpeedMember.Set(sys, speed);
    }
    public static IEnumerable<PlanetarySystem> AllPlanetarySystems() {
        object pm = PlayerManagerMember.Get(null);
        if(pm != null && PlayersMember.Get(pm) is IEnumerable players) {
            Refl.Member perPlayer = null;
            foreach(object p in players) {
                if(p == null) continue;
                perPlayer ??= new Refl.Member(p.GetType(), "planetarySystem");
                if(perPlayer.Get(p) is PlanetarySystem sys) yield return sys;
            }
            yield break;
        }
        if(Planetary(scrController.instance) is PlanetarySystem only) yield return only;
    }
    private static readonly Refl.Member EditorPausedInPlayMode =
        new(typeof(scnEditor), "pausedInPlayMode");
    public static void ClearEditorPlayModePause(scnEditor editor) =>
        EditorPausedInPlayMode.Set(editor, false);
    private static readonly Type FloorHelperType = Refl.Type("FloorHelper");
    private static readonly MethodInfo TransformCharMethod = FindTransform(typeof(char));
    private static readonly MethodInfo TransformFloatMethod = FindTransform(typeof(float));
    private static readonly object RotateCw90Op = TransformOperation("ClockwiseRotation90");
    private static readonly object RotateCcw90Op = TransformOperation("ClockwiseRotation270");
    private static readonly MethodInfo LegacyRotCharMethod = FindLegacyRot(typeof(char));
    private static readonly MethodInfo LegacyRotFloatMethod = FindLegacyRot(typeof(float));
    private static MethodInfo FindTransform(Type directionType) =>
        FindByFirstArg(FloorHelperType, "Transform", directionType, 2);
    private static MethodInfo FindLegacyRot(Type directionType) =>
        FindByFirstArg(typeof(scrLevelMaker), "GetRotDirection", directionType, 2);
    private static MethodInfo FindByFirstArg(Type owner, string name, Type firstArg, int argCount) {
        if(owner == null) return null;
        try {
            foreach(MethodInfo m in owner.GetMethods(Refl.Any)) {
                if(m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if(ps.Length == argCount && ps[0].ParameterType == firstArg) return m;
            }
        } catch { }
        return null;
    }
    private static object TransformOperation(string name) {
        try {
            Type op = FloorHelperType?.GetNestedType("TransformOperation", Refl.Any);
            return op is { IsEnum: true } ? Enum.Parse(op, name) : null;
        } catch {
            return null;
        }
    }
    public static char RotateDirection(char direction, bool clockwise) {
        object op = clockwise ? RotateCw90Op : RotateCcw90Op;
        if(TransformCharMethod != null && op != null)
            return Refl.Invoke(TransformCharMethod, null, direction, op) as char? ?? direction;
        return Refl.Invoke(LegacyRotCharMethod, scrLevelMaker.instance, direction, clockwise)
            as char? ?? direction;
    }
    public static float RotateDirection(float direction, bool clockwise) {
        object op = clockwise ? RotateCw90Op : RotateCcw90Op;
        if(TransformFloatMethod != null && op != null)
            return Refl.Invoke(TransformFloatMethod, null, direction, op) as float? ?? direction;
        return Refl.Invoke(LegacyRotFloatMethod, scrLevelMaker.instance, direction, clockwise)
            as float? ?? direction;
    }
    public static MethodBase RotateCharTarget => TransformCharMethod ?? LegacyRotCharMethod;
    public static MethodBase RotateFloatTarget => TransformFloatMethod ?? LegacyRotFloatMethod;
    private static readonly MethodInfo ShowSteamTextInputMethod = Refl.Method(
        Refl.Type("ADOFAI.SteamIntegration.SteamWorkshop") ?? Refl.Type("SteamWorkshop"),
        "ShowTextInput", 0);
    public static bool? ShowSteamTextInput() =>
        ShowSteamTextInputMethod == null ? null : Refl.Invoke(ShowSteamTextInputMethod, null) as bool?;
    private static readonly Refl.Member BundleLevelMember = new(typeof(ADOBase), "isBundleLevel");
    public static bool IsBundleLevel() => BundleLevelMember.Get(null, false);
    private static readonly Refl.Member WorldDataMember = new(Refl.Type("GCNS"), "worldData");
    private static readonly Refl.Member WorldDataDictMember =
        new(Refl.Type("WorldData"), "dict");
    public static IDictionary WorldTable() =>
        (WorldDataDictMember.Exists ? WorldDataDictMember.Get(null) : WorldDataMember.Get(null)) as IDictionary;
    public static int WorldLevelCount(string world) {
        IDictionary table = WorldTable();
        if(string.IsNullOrEmpty(world) || table == null || !table.Contains(world)) return -1;
        object entry = table[world];
        return entry == null ? -1 : new Refl.Member(entry.GetType(), "levelCount").Get(entry, -1);
    }
    public static bool IsInternalLevelCode(string code) {
        MethodInfo direct = Refl.Method(typeof(scrController), "IsWorldAndLevelInternalLevel", 1);
        if(direct != null) return Refl.Invoke(direct, null, code) is bool b && b;
        if(string.IsNullOrEmpty(code)) return false;
        int dash = code.IndexOf('-');
        string world = dash > 0 ? code[..dash] : code;
        IDictionary table = WorldTable();
        if(table == null || !table.Contains(world)) return false;
        object entry = table[world];
        return entry != null && new Refl.Member(entry.GetType(), "internalLevel").Get(entry, false);
    }
    private static readonly Refl.Member LoaderMember = new(typeof(ADOBase), "loader");
    public static bool LoadSceneWithTransition(WipeDirection direction) {
        object loader = LoaderMember.Get(null);
        MethodInfo m = loader == null ? null : Refl.Method(loader.GetType(), "LoadSceneWithTransition", 1);
        if(m != null) {
            Refl.Invoke(m, loader, direction);
            return true;
        }
        scrController ctrl = scrController.instance;
        MethodInfo start = ctrl == null ? null : Refl.Method(typeof(scrController), "StartLoadingScene", 1);
        if(start == null) return false;
        Refl.Invoke(start, ctrl, direction);
        return true;
    }
    private static readonly MethodInfo HookKeyToUnityKeyMethod = ResolveHookKeyMapper();
    private static MethodInfo ResolveHookKeyMapper() {
        try {
            Assembly skyhook = typeof(SkyHook.SkyHookManager).Assembly;
            Type t = skyhook.GetType("SkyHook.SkyHookKeyMapper") ?? skyhook.GetType("SkyHook.AsyncKeyMapper");
            return Refl.Method(t, "SkyHookKeyToUnityKey", 1) ?? Refl.Method(t, "AsyncKeyToUnityKey", 1);
        } catch {
            return null;
        }
    }
    public static KeyCode HookKeyToUnityKey(SkyHook.KeyLabel label) =>
        Refl.Invoke(HookKeyToUnityKeyMethod, null, label) as KeyCode? ?? KeyCode.None;
    private static readonly MethodInfo RdStringGet = Refl.Method(typeof(RDString), "Get", 1);
    public static string GameString(string key) =>
        Refl.Invoke(RdStringGet, null, key) as string ?? key;
    private static readonly MethodInfo FindOrLoadClip =
        Refl.Method(typeof(AudioManager), "FindOrLoadAudioClip", 1);
    public static AudioClip FindAudioClip(string clipName) =>
        Refl.Invoke(FindOrLoadClip, AudioManager.Instance, clipName) as AudioClip;
    public static MethodBase OnDamageTarget => Refl.Method(PlayerStateType, "OnDamage");
    private static readonly MethodInfo LevelEventGetMethod =
        Refl.Method(typeof(ADOFAI.LevelEvent), "Get", 1);
    private static readonly MethodInfo LevelEventContainsMethod =
        Refl.Method(typeof(ADOFAI.LevelEvent), "ContainsKey", 1);
    private static readonly Refl.Member LevelEventDataMember =
        new(typeof(ADOFAI.LevelEvent), "data");
    private static class EventGetter<T> {
        internal static readonly MethodInfo Bound = Bind();
        private static MethodInfo Bind() {
            try {
                return LevelEventGetMethod is { IsGenericMethodDefinition: true }
                    ? LevelEventGetMethod.MakeGenericMethod(typeof(T))
                    : null;
            } catch {
                return null;
            }
        }
    }
    public static T EventGet<T>(ADOFAI.LevelEvent ev, string key) =>
        ev == null ? default : Refl.Invoke(EventGetter<T>.Bound, ev, key) is T t ? t : default;
    private static readonly Refl.Member SceneLevelSelectMember =
        new(Refl.Type("GCNS"), "sceneLevelSelect");
    private static readonly Refl.Member CameraInstanceMember = new(typeof(scrCamera), "instance");
    private static readonly Refl.Member FontAssetMaterialMember =
        new(typeof(TMPro.TMP_Asset), "material");
    public static string SceneLevelSelect =>
        SceneLevelSelectMember.Get(null, "scnLevelSelect");
    public static scrCamera Camera => CameraInstanceMember.Get(null) as scrCamera;
    public static Material FontMaterial(TMPro.TMP_Asset asset) =>
        asset == null ? null : FontAssetMaterialMember.Get(asset) as Material;
    private static readonly Refl.Member PlanetRingMember = new(typeof(PlanetRenderer), "ring");
    public static Renderer PlanetRing(PlanetRenderer renderer) =>
        renderer == null ? null : PlanetRingMember.Get(renderer) as Renderer;
    public static bool TryGetRingColor(PlanetRenderer renderer, out Color color) {
        switch(PlanetRing(renderer)) {
            case LineRenderer line:
                color = line.startColor;
                return true;
            case SpriteRenderer sprite:
                color = sprite.color;
                return true;
            default:
                color = default;
                return false;
        }
    }
    public static void SetRingColor(PlanetRenderer renderer, Color color) {
        switch(PlanetRing(renderer)) {
            case LineRenderer line:
                if(line.startColor != color) line.startColor = color;
                if(line.endColor != color) line.endColor = color;
                break;
            case SpriteRenderer sprite:
                if(sprite.color != color) sprite.color = color;
                break;
        }
    }
    private static readonly Refl.Member ResultsReadoutMember =
        new(typeof(scrUIController), "txtResults");
    public static GameObject ResultsReadout(scrUIController ui) =>
        ui == null ? null : (ResultsReadoutMember.Get(ui) as Component)?.gameObject;
    private static readonly Refl.Member HitTextLabelMember = new(typeof(scrHitTextMesh), "text");
    private static Refl.Member hitTextStringMember;
    public static TMPro.TMP_Text HitTextLabel(scrHitTextMesh mesh) =>
        mesh == null ? null : HitTextLabelMember.Get(mesh) as TMPro.TMP_Text;
    public static void ClearHitTextLabel(scrHitTextMesh mesh) {
        object label = mesh == null ? null : HitTextLabelMember.Get(mesh);
        if(label == null) return;
        hitTextStringMember ??= new Refl.Member(label.GetType(), "text");
        hitTextStringMember.Set(label, "");
    }
    public static MethodBase MenuSpeedStartEffectTarget =>
        AccessTools.Method(typeof(ffxMenuPlanetSpeedChange), "StartEffect", new[] { typeof(scrPlanet) })
        ?? AccessTools.Method(typeof(ffxMenuPlanetSpeedChange), "StartEffect", Type.EmptyTypes)
        ?? Refl.Method(typeof(ffxMenuPlanetSpeedChange), "StartEffect");
    public static MethodBase PlanetPaletteMethod(string name) =>
        Refl.Method(typeof(PlanetarySystem), name) ?? Refl.Method(typeof(scnLevelSelect), name);
    public static bool EventHas(ADOFAI.LevelEvent ev, string key) {
        if(ev == null) return false;
        if(LevelEventContainsMethod != null)
            return Refl.Invoke(LevelEventContainsMethod, ev, key) is bool b && b;
        return LevelEventDataMember.Get(ev) is IDictionary data && data.Contains(key);
    }
    private static readonly Type PlayerType = Refl.Type("scrPlayer");
    private static readonly Type HitTextManagerType = Refl.Type("scrHitTextManager");
    public static MethodBase CountValidKeysPressedTarget =>
        Refl.Method(PlayerType ?? typeof(scrController), "CountValidKeysPressed", 0);
    public static MethodBase PlayerControlUpdateTarget =>
        Refl.Method(PlayerType ?? typeof(scrController), "Simulated_PlayerControl_Update");
    public static MethodBase ShowHitTextTarget => Refl.Method(HitTextManagerType, "ShowHitText");
    public static MethodBase LegacyShowHitTextTarget =>
        HitTextManagerType != null ? null : Refl.Method(typeof(scrController), "ShowHitText", 3);
    public static MethodBase SwitchChosenTarget => Refl.Method(typeof(scrPlanet), "SwitchChosen", 0);
    private static readonly Type CalibrationType =
        Refl.Type("scnCalibration") ?? Refl.Type("scrCalibrationPlanet");
    public static bool HasCalibrationScreen => CalibrationType != null;
    public static MethodBase CalibrationMethod(string name) => Refl.Method(CalibrationType, name);
    private static readonly Refl.Member CalibrationOffsetsMember = new(CalibrationType, "listOffsets");
    private static readonly Refl.Member CalibrationDoneMember = new(CalibrationType, "calibrated");
    public static object CalibrationOffsets(object screen) => CalibrationOffsetsMember.Get(screen);
    public static bool CalibrationDone(object screen) => CalibrationDoneMember.Get(screen, false);
    public static double AverageOffset(object listOffsets, out int count) {
        count = 0;
        if(listOffsets is not System.Collections.IEnumerable items) return 0.0;
        double sum = 0.0;
        Refl.Member offsetMember = null;
        foreach(object item in items) {
            if(item == null) continue;
            if(item is double d) {
                sum += d;
            } else {
                offsetMember ??= new Refl.Member(item.GetType(), "offset");
                sum += offsetMember.Get(item, 0.0);
            }
            count++;
        }
        return count == 0 ? 0.0 : sum / count;
    }
    private static readonly Refl.Member AsyncKeyboardMember =
        new(typeof(RDInput), "asyncKeyboard", "asyncKeyboardMouseInput");
    private static readonly Refl.Member InputActiveMember = new(typeof(RDInputType), "isActive");
    public static bool AsyncKeyboardActive() {
        object input = AsyncKeyboardMember.Get(null);
        return input != null && InputActiveMember.Get(input, false);
    }
    public static void LogDetectedVersion() =>
        MainCore.Log.Msg($"[Compat] ADOFAI {GameVersion.DisplayRelease}"
            + (GameVersion.IsLegacy ? " — legacy (pre-3.0) game API" : " — current game API"));
}
