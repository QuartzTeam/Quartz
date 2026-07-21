using System.Collections;
using System.Reflection;
using Quartz.Core;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    private static readonly Type OptionsPanelsCLS = AccessTools.TypeByName("OptionsPanelsCLS");
    internal static InputField clsSearchField;
    internal static CanvasGroup clsSearchFieldCanvasGroup;
    internal static bool clsSearchMode;
    private static Sequence clsSearchSeq;
    static partial void ToggleLegacyCLSImpl(bool active) => ToggleCLS(active);
    private static object OptionsPanels =>
        scnCLS.instance == null ? null : Traverse.Create(scnCLS.instance).Field("optionsPanels").GetValue();
    private static void CreateInputField() {
        if(clsSearchField || scnCLS.instance == null) return;
        NostalgiaImages.EnsureLoaded();
        GameObject container = new("Search Field Container");
        container.transform.SetParent(scnCLS.instance.transform, false);
        RectTransform crect = container.GetOrAddComponent<RectTransform>();
        crect.offsetMax = new Vector2(276.25f, 0);
        crect.offsetMin = new Vector2(-276.25f, -100.7f);
        crect.pivot = new Vector2(0.5f, 1);
        crect.localPosition = new Vector3(0, 0, 2.5098f);
        crect.sizeDelta = new Vector2(552.5f, 100.7f);
        container.GetOrAddComponent<CanvasRenderer>().cullTransparentMesh = false;
        GameObject field = new("Search Field");
        field.transform.SetParent(container.transform, false);
        RectTransform frect = field.GetOrAddComponent<RectTransform>();
        frect.localPosition = new Vector3(0, -50.35f, 0);
        frect.offsetMax = new Vector2(276, 50);
        frect.offsetMin = new Vector2(-276, -50);
        frect.sizeDelta = new Vector2(552, 100);
        field.GetOrAddComponent<CanvasRenderer>().cullTransparentMesh = false;
        Image image = field.AddComponent<Image>();
        image.sprite = NostalgiaImages.EditorButtonFill;
        image.type = Image.Type.Sliced;
        InputField inputField = field.AddComponent<InputField>();
        inputField.caretColor = new Color(0.1961f, 0.1961f, 0.1961f);
        inputField.image = image;
        inputField.targetGraphic = image;
        clsSearchField = inputField;
        clsSearchFieldCanvasGroup = field.AddComponent<CanvasGroup>();
        inputField.placeholder = MakeFieldText(field.transform, "Placeholder", true);
        inputField.textComponent = MakeFieldText(field.transform, "Text", false);
        field.SetActive(false);
        clsSearchField.onValueChanged.AddListener(sub => scnCLS.instance.SearchLevels(sub, true));
        clsSearchField.onEndEdit.AddListener(_ => ToggleSearchModeCLS(false));
    }
    private static void SetClsResponsive(bool value) {
        try { Traverse.Create(scnCLS.instance).Property("responsive").SetValue(value); } catch { }
    }
    private static Text MakeFieldText(Transform parent, string name, bool placeholder) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetOrAddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, -0.5f);
        rect.anchorMax = new Vector2(1, 1);
        rect.anchorMin = new Vector2(0, 0);
        rect.offsetMax = new Vector2(-10, -7);
        rect.offsetMin = new Vector2(10, 6);
        rect.sizeDelta = new Vector2(-20, -13);
        obj.GetOrAddComponent<CanvasRenderer>().cullTransparentMesh = false;
        Text text = obj.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 60;
        text.lineSpacing = 1;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        if(placeholder) {
            text.fontStyle = FontStyle.Italic;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.supportRichText = true;
            text.text = GameApi.GameString("cls.find");
            text.color = new Color(0.1961f, 0.1961f, 0.1961f, 0.5f);
        } else {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            text.text = "";
            text.color = new Color(0.1961f, 0.1961f, 0.1961f);
        }
        text.SetLocalizedFont();
        return text;
    }
    internal static void ToggleSearchModeCLS(bool search) {
        if(scnCLS.instance != null) scnCLS.instance.StartCoroutine(ToggleSearchModeCo(search));
    }
    private static IEnumerator ToggleSearchModeCo(bool search) {
        Traverse.Create(OptionsPanels).Field("searchMode").SetValue(clsSearchMode = search);
        if(search && RDC.runningOnSteamDeck) {
            while(GameApi.ShowSteamTextInput() == false) yield return null;
        }
        clsSearchSeq?.Kill(false);
        const float duration = 0.33f;
        clsSearchSeq = DOTween.Sequence()
            .Insert(0f, clsSearchFieldCanvasGroup.DOFade(search ? 1f : 0f, duration).SetEase(Ease.OutCubic))
            .Insert(0f, clsSearchField.GetComponent<RectTransform>().DOPivotY(search ? 0f : 0.5f, duration).SetEase(Ease.OutCubic));
        if(search) {
            clsSearchField.gameObject.SetActive(true);
            SetClsResponsive(false);
            clsSearchField.ActivateInputField();
            yield break;
        }
        clsSearchSeq.OnComplete(() => clsSearchField.gameObject.SetActive(false));
        yield return new WaitForEndOfFrame();
        SetClsResponsive(true);
    }
    private static void ToggleCLS(bool active) {
        if(OptionsPanelsCLS == null || scnCLS.instance == null || (!active && !clsSearchField)) return;
        try {
            object optionsPanels = OptionsPanels;
            ADOBase optionsPanelsBase = optionsPanels as ADOBase;
            if(active) {
                scnCLS.instance.gameObject.GetOrAddComponent<WorkshopShortcut>();
                CreateInputField();
                if(Traverse.Create(optionsPanels).Field("showingLeftPanel").GetValue<bool>())
                    Traverse.Create(optionsPanels).Method("TogglePanel", true, false).GetValue();
                if(Traverse.Create(optionsPanels).Field("showingRightPanel").GetValue<bool>())
                    Traverse.Create(optionsPanels).Method("TogglePanel", false, false).GetValue();
                clsSearchField.text = Traverse.Create(optionsPanels).Field("searchInputField").GetValue<InputField>().text;
            } else {
                ToggleSearchModeCLS(false);
                InputField modern = Traverse.Create(optionsPanels).Field("searchInputField").GetValue<InputField>();
                modern.text = clsSearchField.text;
            }
            if(optionsPanelsBase != null) optionsPanelsBase.gameObject.SetActive(!active);
            scnCLS.instance.transform.Find("LevelInfoCanvas")?.Find("HelpContainer")?.gameObject.SetActive(active);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Nostalgia] LegacyCLS toggle failed: {e.Message}");
        }
    }
    [HarmonyPatch(typeof(scnCLS), "Awake")]
    private static class LegacyClsAwakePatch {
        private static bool Prepare() => OptionsPanelsCLS != null;
        private static void Postfix() {
            if(!Enabled) return;
            try {
                scnCLS.instance.gameObject.GetOrAddComponent<WorkshopShortcut>();
                Transform helpOrder = scnCLS.instance.transform
                    .Find("LevelInfoCanvas")?.Find("HelpContainer")?.Find("HelpOrder");
                if(helpOrder != null) {
                    Component changer = helpOrder.GetComponent("scrTextChanger") as Component;
                    if(changer != null) Object.Destroy(changer);
                }
                Traverse.Create(OptionsPanels).Method("UpdateOrderText").GetValue();
                ToggleCLS(ShouldLegacyCLS);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[Nostalgia] LegacyCLS awake failed: {e.Message}");
            }
        }
    }
    [HarmonyPatch]
    private static class LegacyClsToggleSearchPatch {
        private static MethodBase TargetMethod() => AccessTools.Method(OptionsPanelsCLS, "ToggleSearchMode");
        private static bool Prepare() => AccessTools.Method(OptionsPanelsCLS, "ToggleSearchMode") != null;
        private static bool Prefix(bool search) {
            if(!ShouldLegacyCLS) return true;
            ToggleSearchModeCLS(search);
            return false;
        }
    }
}
public sealed class WorkshopShortcut : MonoBehaviour {
    private AccessTools.FieldRef<scnCLS, object> optionsPanelsRef;
    private Func<scnCLS, bool> responsiveGetter;
    private Action<object> toggleSpeedTrial;
    private Action<object> toggleNoFail;
    private void Awake() {
        try { optionsPanelsRef = AccessTools.FieldRefAccess<scnCLS, object>("optionsPanels"); } catch { }
        try {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(scnCLS), "responsive");
            if(getter != null) responsiveGetter = (Func<scnCLS, bool>)Delegate.CreateDelegate(typeof(Func<scnCLS, bool>), getter);
        } catch { }
    }
    private void Update() {
        if(!Nostalgia.ShouldLegacyCLS
           || scnCLS.instance == null
           || scrController.instance == null
           || scrController.instance.paused
           || Nostalgia.clsSearchMode
           || !IsResponsive(scnCLS.instance)
           || scnCLS.instance.showingInitialMenu) {
            return;
        }
        if(Input.GetKeyDown(KeyCode.F)) {
            Nostalgia.ToggleSearchModeCLS(true);
        } else if(Input.GetKeyDown(KeyCode.S)) {
            object optionsPanels = GetOptionsPanels();
            (toggleSpeedTrial ??= BuildActionInvoker(optionsPanels, "ToggleSpeedTrial"))(optionsPanels);
        } else if(Input.GetKeyDown(KeyCode.N)) {
            object optionsPanels = GetOptionsPanels();
            (toggleNoFail ??= BuildActionInvoker(optionsPanels, "ToggleNoFail"))(optionsPanels);
        } else if(Input.GetKeyDown(KeyCode.Delete)) {
            scnCLS.instance.DeleteLevel();
        } else if(Input.GetKeyDown(KeyCode.O)) {
            try {
                object optionsPanels = GetOptionsPanels();
                var t = Traverse.Create(optionsPanels);
                Array sortings = t.Field("sortings").GetValue<Array>();
                object current = t.Field("sortingMethod").GetValue();
                int num = Array.IndexOf(sortings, current);
                num = (num == sortings.Length - 1) ? 0 : num + 1;
                object next = sortings.GetValue(num);
                t.Field("sortingMethod").SetValue(next);
                t.Method("SelectOption", [next, true]).GetValue();
                t.Method("UpdateSorting").GetValue();
            } catch { }
        }
    }
    private object GetOptionsPanels() =>
        optionsPanelsRef != null
            ? optionsPanelsRef(scnCLS.instance)
            : Traverse.Create(scnCLS.instance).Field("optionsPanels").GetValue();
    private bool IsResponsive(scnCLS cls) =>
        responsiveGetter != null
            ? responsiveGetter(cls)
            : Traverse.Create(cls).Property("responsive").GetValue<bool>();
    private static Action<object> BuildActionInvoker(object optionsPanels, string methodName) {
        try {
            MethodInfo method = optionsPanels != null ? AccessTools.Method(optionsPanels.GetType(), methodName) : null;
            if(method != null) return target => method.Invoke(target, null);
        } catch { }
        return target => Traverse.Create(target).Method(methodName).GetValue();
    }
}
