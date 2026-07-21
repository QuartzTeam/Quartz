using System.Reflection;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Quartz.Features.UiHider;
public static partial class UiHider {
    public static SettingsFile<UiHiderSettings> ConfMgr { get; private set; }
    public static UiHiderSettings Conf => ConfMgr?.Data;
    internal static readonly Vector3 HiddenPosition = new(123456f, 123456f, 123456f);
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<UiHiderSettings>.Loaded("UiHider.json");
        EnsureTicker();
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static bool IsFeatureActive() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf.Enabled;
    }
    internal static UiHiderProfile SelectedProfile
        => Conf.RecordingMode ? Conf.Recording : Conf.Playing;
    public static void ApplyNow() {
        EnsureConf();
        ShowOrHideElements();
    }
    public static void Restore() => ShowOrHideElements(true);
    public static void ToggleRecordingMode() {
        EnsureConf();
        Conf.RecordingMode = !Conf.RecordingMode;
        Save();
        ShowOrHideElements();
    }
    private static bool lastActive = true;
    private static void TickInternal() {
        if(!MainCore.IsModEnabled) {
            if(lastActive) {
                Restore();
                lastActive = false;
            }
            return;
        }
        if(ShouldToggleRecordingMode()) ToggleRecordingMode();
        bool active = IsFeatureActive();
        if(!active && !lastActive) {
            return;
        }
        ShowOrHideElements();
        lastActive = active;
    }
    private static void ShowOrHideElements(bool forceDisabled = false) {
        EnsureConf();
        bool tweakEnabled = !forceDisabled && IsFeatureActive();
        UiHiderProfile profile = tweakEnabled ? SelectedProfile : null;
        bool hideEverything = profile != null && profile.HideEverything;
        bool hideOtto = profile != null && (hideEverything || profile.HideOtto);
        bool hideTimingTarget = profile != null && (hideEverything || profile.HideTimingTarget);
        bool hideNoFail = profile != null && (hideEverything || profile.HideNoFailIcon);
        bool hideBeta = profile != null && (hideEverything || profile.HideBeta);
        bool hideTitle = profile != null && (hideEverything || profile.HideTitle);
        bool hideMeter = profile != null && (hideEverything || profile.HideHitErrorMeter);
        try { RDC.noHud = hideEverything; } catch { }
        ReconcileHitErrorMeter(hideMeter);
        scrUIController uiController = scrUIController.instance;
        if(uiController == null) return;
        if(IsEditingLevel() || scnEditor.instance != null) {
            object editor = scnEditor.instance;
            if(editor is scnEditor ed && !ed.playMode) HideGameplayDifficultyContainer(uiController);
            SetEnabled(GetMemberValueCached(editor, "autoImage"), !hideOtto);
            SetEnabled(GetMemberValueCached(editor, "buttonAuto"), !hideOtto);
            SetMemberGameObjectActiveIfMatches(editor, "editorDifficultySelector", hideTimingTarget);
            SetMemberGameObjectActiveIfMatches(editor, "buttonNoFail", hideNoFail);
        } else {
            SetEnabled(uiController.noFailImage, !hideNoFail);
            SetEnabled(uiController.difficultyImage, !hideTimingTarget);
            SetGameObjectActiveIfMatches(
                uiController.difficultyContainer != null ? uiController.difficultyContainer.gameObject : null,
                hideTimingTarget);
            SetMemberGameObjectActiveIfMatches(uiController, "difficultyFadeContainer", hideTimingTarget);
        }
        if(HasSteamBranchName()) SetBetaObjectsActiveIfMatches(hideBeta);
        SetGameObjectActiveIfMatches(
            uiController.txtLevelName != null ? uiController.txtLevelName.gameObject : null,
            hideTitle);
    }
    internal static bool ShouldHideJudgementText()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideJudgment);
    internal static bool ShouldHideMissIndicators()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideMissIndicators);
    internal static bool ShouldHideOtto()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideOtto);
    internal static bool ShouldHideResult()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideResult);
    internal static bool ShouldHideHitErrorMeter()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideHitErrorMeter);
    internal static bool ShouldHideLastFloorFlash()
        => IsFeatureActive() && (SelectedProfile.HideEverything || SelectedProfile.HideLastFloorFlash);
    private static bool ShouldToggleRecordingMode() {
        if(!Conf.Enabled || !Conf.UseShortcut || Keybind.Capturing) return false;
        KeyCode key = (KeyCode)Conf.ShortcutKey;
        if(key == KeyCode.None) return false;
        try {
            return Keybind.ModifierHeld((Keybind.KeyModifier)Conf.ShortcutModifier)
                && Input.GetKeyDown(key);
        } catch {
            return false;
        }
    }
    private static PropertyInfo isEditingLevelProperty;
    private static MethodInfo isEditingLevelGetter;
    private static bool isEditingLevelGetterStatic;
    private static Func<bool> isEditingLevelStaticFunc;
    private static bool reflectionReady;
    private static bool IsEditingLevel() {
        EnsureReflection();
        if(isEditingLevelProperty != null && isEditingLevelGetter != null) {
            try {
                if(isEditingLevelGetterStatic) {
                    if(isEditingLevelStaticFunc != null) return isEditingLevelStaticFunc();
                    return Convert.ToBoolean(isEditingLevelProperty.GetValue(null, null));
                }
                object target = scnEditor.instance;
                if(target != null) return Convert.ToBoolean(isEditingLevelProperty.GetValue(target, null));
            } catch { }
        }
        return scnEditor.instance != null && scnGame.instance == null;
    }
    private static void EnsureReflection() {
        if(reflectionReady) return;
        reflectionReady = true;
        Type adoBase = AccessTools.TypeByName("ADOBase");
        if(adoBase != null) {
            isEditingLevelProperty = AccessTools.Property(adoBase, "isEditingLevel");
            if(isEditingLevelProperty != null) {
                isEditingLevelGetter = isEditingLevelProperty.GetGetMethod(true);
                isEditingLevelGetterStatic = isEditingLevelGetter != null && isEditingLevelGetter.IsStatic;
                if(isEditingLevelGetterStatic) {
                    try {
                        isEditingLevelStaticFunc =
                            (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isEditingLevelGetter);
                    } catch {
                        isEditingLevelStaticFunc = null;
                    }
                }
            }
        }
    }
    private static readonly Dictionary<(Type, string), MemberInfo> memberCache = [];
    private static readonly Dictionary<(int, string), UnityEngine.Object> memberValueCache = [];
    internal static void ClearMemberValueCache() => memberValueCache.Clear();
    internal static object GetMemberValueCached(object owner, string memberName) {
        if(owner is not UnityEngine.Object unityOwner) return GetMemberValue(owner, memberName);
        (int, string) key = (unityOwner.GetInstanceID(), memberName);
        if(memberValueCache.TryGetValue(key, out UnityEngine.Object cached) && cached != null) return cached;
        object value = GetMemberValue(owner, memberName);
        if(value is UnityEngine.Object unityValue && unityValue != null) memberValueCache[key] = unityValue;
        return value;
    }
    internal static object GetMemberValue(object owner, string memberName) {
        if(owner == null || string.IsNullOrEmpty(memberName)) return null;
        Type type = owner.GetType();
        var key = (type, memberName);
        if(!memberCache.TryGetValue(key, out MemberInfo member)) {
            member = (MemberInfo)AccessTools.Field(type, memberName)
                  ?? AccessTools.Property(type, memberName);
            memberCache[key] = member;
        }
        try {
            if(member is FieldInfo field) return field.GetValue(owner);
            if(member is PropertyInfo property) return property.GetValue(owner, null);
        } catch { }
        return null;
    }
    internal static GameObject GetGameObject(object value) {
        if(value == null) return null;
        if(value is GameObject gameObject) return gameObject;
        return value is Component component ? component.gameObject : null;
    }
    private static void SetEnabled(object value, bool enabled) {
        if(value == null) return;
        if(value is Behaviour behaviour) {
            if(behaviour.enabled != enabled) behaviour.enabled = enabled;
            return;
        }
        PropertyInfo property = AccessTools.Property(value.GetType(), "enabled");
        if(property == null || !property.CanWrite) return;
        try { property.SetValue(value, enabled, null); } catch { }
    }
    private static void SetMemberGameObjectActiveIfMatches(object owner, string memberName, bool hide)
        => SetGameObjectActiveIfMatches(GetGameObject(GetMemberValueCached(owner, memberName)), hide);
    private static void SetGameObjectActiveIfMatches(GameObject gameObject, bool hide) {
        if(gameObject == null) return;
        if(gameObject.activeSelf == hide) gameObject.SetActive(!hide);
    }
    internal static void HideGameplayDifficultyContainer(scrUIController uiController) {
        if(uiController == null) return;
        try {
            if(uiController.difficultyContainer != null && uiController.difficultyContainer.gameObject.activeSelf)
                uiController.difficultyContainer.gameObject.SetActive(false);
            if(uiController.difficultyFadeContainer != null) {
                if(uiController.difficultyFadeContainer.blocksRaycasts)
                    uiController.difficultyFadeContainer.blocksRaycasts = false;
                if(uiController.difficultyFadeContainer.gameObject.activeSelf)
                    uiController.difficultyFadeContainer.gameObject.SetActive(false);
            }
            if(uiController.difficultyButtonLeft != null && uiController.difficultyButtonLeft.enabled)
                uiController.difficultyButtonLeft.enabled = false;
            if(uiController.difficultyButtonRight != null && uiController.difficultyButtonRight.enabled)
                uiController.difficultyButtonRight.enabled = false;
        } catch { }
    }
    private static void ReconcileHitErrorMeter(bool hide) {
        scrController controller = scrController.instance;
        if(controller == null || !controller.gameworld) return;
        GameObject errorMeter = GetGameObject(GetMemberValueCached(controller, "errorMeter"));
        if(errorMeter == null) return;
        if(hide) {
            if(errorMeter.activeSelf) errorMeter.SetActive(false);
            return;
        }
        if(!controller.paused && HitErrorMeterEnabledInGame() && !errorMeter.activeSelf) errorMeter.SetActive(true);
    }
    private static bool HitErrorMeterEnabledInGame() {
        try { return Persistence.hitErrorMeterSize != ErrorMeterSize.Off; }
        catch { return true; }
    }
    private static bool HasSteamBranchName() {
        try { return !string.IsNullOrEmpty(GCS.steamBranchName); }
        catch { return false; }
    }
    private static Type betaType;
    private static bool betaTypeResolved;
    private static UnityEngine.Object[] cachedBetaObjects;
    private static int cachedBetaSceneHandle;
    private static void SetBetaObjectsActiveIfMatches(bool hide) {
        if(!betaTypeResolved) {
            betaType = AccessTools.TypeByName("scrEnableIfBeta");
            betaTypeResolved = true;
        }
        if(betaType == null) return;
        int scene = SceneManager.GetActiveScene().GetHashCode();
        if(cachedBetaObjects == null || cachedBetaSceneHandle != scene) {
            try { cachedBetaObjects = Resources.FindObjectsOfTypeAll(betaType); }
            catch { cachedBetaObjects = null; }
            cachedBetaSceneHandle = scene;
        }
        if(cachedBetaObjects == null) return;
        for(int i = 0; i < cachedBetaObjects.Length; i++)
            SetGameObjectActiveIfMatches(GetGameObject(cachedBetaObjects[i]), hide);
    }
    private static Ticker ticker;
    private static void EnsureTicker() {
        if(ticker != null || MainCore.Root == null) return;
        ticker = MainCore.Root.AddComponent<Ticker>();
    }
    private sealed class Ticker : MonoBehaviour {
        private void Update() => TickInternal();
    }
}
