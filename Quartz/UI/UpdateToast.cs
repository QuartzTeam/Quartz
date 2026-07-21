using GTweens.Builders;
using GTweens.Easings;
using GTweens.Tweens;
using Quartz.Core;
using Quartz.Localization;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI.Factory;
using Quartz.UI.Factory.Page;
using Quartz.Update;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
namespace Quartz.UI;
public static class UpdateToast {
    private const float WIDTH = 360f;
    private const float HEIGHT = 64f;
    private const float AUTO_HIDE_SECONDS = 10f;
    private static readonly Vector2 ShownPos = new(-24f, -24f);
    private static readonly Vector2 HiddenPos = new(WIDTH + 24f, -24f);
    private static GameObject canvasObj;
    private static GameObject toastObj;
    private static RectTransform toastRect;
    private static CanvasGroup group;
    private static Image bg;
    private static TextMeshProUGUI titleText;
    private static TextMeshProUGUI hintText;
    private static GTween moveSeq;
    private static GTween hoverSeq;
    private static GTween autoHideSeq;
    private static bool visible;
    private static string shownTag;
    private static string shownName;
    private static string dismissedTag;
    private static Action<string> _onLanguageChanged;
    public static void Initialize() {
        if(canvasObj != null) return;
        Build();
        UpdateService.OnChanged += Refresh;
        _onLanguageChanged = _ => RenderText();
        MainCore.Tr.OnLanguageChanged += _onLanguageChanged;
        Refresh();
    }
    private static void Build() {
        canvasObj = new GameObject("QuartzUpdateToastCanvas");
        canvasObj.transform.SetParent(MainCore.Root.transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;
        canvas.pixelPerfect = true;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UICore.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        toastObj = new GameObject("UpdateToast");
        toastObj.transform.SetParent(canvasObj.transform, false);
        toastRect = toastObj.AddComponent<RectTransform>();
        toastRect.anchorMin = new(1f, 1f);
        toastRect.anchorMax = new(1f, 1f);
        toastRect.pivot = new(1f, 1f);
        toastRect.sizeDelta = new(WIDTH, HEIGHT);
        toastRect.anchoredPosition = HiddenPos;
        group = toastObj.AddComponent<CanvasGroup>();
        bg = toastObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        bg.type = Image.Type.Sliced;
        bg.color = UIColors.TopBar;
        {
            GameObject border = new("Border");
            border.transform.SetParent(toastObj.transform, false);
            RectTransform rect = border.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new(-2f, -2f);
            rect.offsetMax = new(2f, 2f);
            Image borderImg = border.AddComponent<Image>();
            borderImg.sprite = MainCore.Spr.GetRing(14.5f, 3f);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;
        }
        var trigger = toastObj.AddComponent<EventTrigger>();
        Utility.UnityUtils.AddClickEvent(trigger, _ => OpenSettings());
        void Add(EventTriggerType type, Action cb) {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => cb());
            trigger.triggers.Add(e);
        }
        Add(EventTriggerType.PointerEnter, () => {
            autoHideSeq?.Kill();
            hoverSeq?.Kill();
            hoverSeq = bg.GTColor(Color.Lerp(UIColors.TopBar, Color.white, 0.12f), 0.2f)
                .SetEasing(Easing.OutSine);
            MainCore.TC.Play(hoverSeq);
        });
        Add(EventTriggerType.PointerExit, () => {
            if(visible) StartAutoHide();
            hoverSeq?.Kill();
            hoverSeq = bg.GTColor(UIColors.TopBar, 0.25f)
                .SetEasing(Easing.OutSine);
            MainCore.TC.Play(hoverSeq);
        });
        {
            GameObject icon = new("Icon");
            icon.transform.SetParent(toastObj.transform, false);
            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = new(0f, 0.5f);
            iconRect.anchorMax = new(0f, 0.5f);
            iconRect.pivot = new(0f, 0.5f);
            iconRect.anchoredPosition = new(16f, 0f);
            iconRect.sizeDelta = new(30f, 30f);
            Image iconImg = icon.AddComponent<Image>();
            iconImg.sprite = MainCore.Spr.Get(UISprite.Gear128, 30f);
            iconImg.raycastTarget = false;
        }
        {
            GameObject title = new("Title");
            title.transform.SetParent(toastObj.transform, false);
            RectTransform rect = title.AddComponent<RectTransform>();
            rect.anchorMin = new(0f, 1f);
            rect.anchorMax = new(1f, 1f);
            rect.pivot = new(0f, 1f);
            rect.offsetMin = new(58f, -34f);
            rect.offsetMax = new(-38f, -10f);
            titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.font = FontManager.Current;
            titleText.fontSize = 16f;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
            titleText.characterSpacing = -3f;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.raycastTarget = false;
        }
        {
            GameObject hint = new("Hint");
            hint.transform.SetParent(toastObj.transform, false);
            RectTransform rect = hint.AddComponent<RectTransform>();
            rect.anchorMin = new(0f, 1f);
            rect.anchorMax = new(1f, 1f);
            rect.pivot = new(0f, 1f);
            rect.offsetMin = new(58f, -54f);
            rect.offsetMax = new(-38f, -34f);
            hintText = hint.AddComponent<TextMeshProUGUI>();
            hintText.font = FontManager.Current;
            hintText.fontSize = 13f;
            hintText.color = new Color(1f, 1f, 1f, 0.6f);
            hintText.alignment = TextAlignmentOptions.Left;
            hintText.verticalAlignment = VerticalAlignmentOptions.Middle;
            hintText.characterSpacing = -3f;
            hintText.overflowMode = TextOverflowModes.Ellipsis;
            hintText.raycastTarget = false;
        }
        {
            GameObject close = new("Close");
            close.transform.SetParent(toastObj.transform, false);
            RectTransform rect = close.AddComponent<RectTransform>();
            rect.anchorMin = new(1f, 0.5f);
            rect.anchorMax = new(1f, 0.5f);
            rect.pivot = new(0.5f, 0.5f);
            rect.anchoredPosition = new(-20f, 0f);
            rect.sizeDelta = new(18f, 18f);
            Image xImg = close.AddComponent<Image>();
            xImg.sprite = MainCore.Spr.Get(UISprite.X128, 18f);
            xImg.color = new Color(1f, 1f, 1f, 0.55f);
            var closeTrigger = close.AddComponent<EventTrigger>();
            Utility.UnityUtils.AddClickEvent(closeTrigger, _ => Dismiss());
            void AddClose(EventTriggerType type, Action cb) {
                var e = new EventTrigger.Entry { eventID = type };
                e.callback.AddListener(_ => cb());
                closeTrigger.triggers.Add(e);
            }
            AddClose(EventTriggerType.PointerEnter, () => xImg.color = Color.white);
            AddClose(EventTriggerType.PointerExit, () => xImg.color = new Color(1f, 1f, 1f, 0.55f));
        }
        toastObj.SetActive(false);
    }
    private static void Refresh() {
        if(canvasObj == null) return;
        UpdateStatus status = UpdateService.Status;
        UpdateInfo info = UpdateService.Available;
        if(status == UpdateStatus.Idle) dismissedTag = null;
        if(status == UpdateStatus.Available && info != null && info.Tag != dismissedTag) Show(info.Tag, info.Name);
        else Hide();
    }
    private static void Show(string tag, string name) {
        if(visible && shownTag == tag) return;
        shownTag = tag;
        shownName = name;
        RenderText();
        bg.color = UIColors.TopBar;
        visible = true;
        toastObj.SetActive(true);
        moveSeq?.Kill();
        moveSeq = GTweenSequenceBuilder.New()
            .Join(toastRect.GTAnchorPos(ShownPos, 0.5f).SetEasing(Easing.OutExpo))
            .Join(group.GTFade(1f, 0.3f).SetEasing(Easing.OutSine))
            .Build();
        MainCore.TC.Play(moveSeq);
        StartAutoHide();
    }
    private static void StartAutoHide() {
        autoHideSeq?.Kill();
        autoHideSeq = GTweenSequenceBuilder.New()
            .AppendTime(AUTO_HIDE_SECONDS)
            .AppendCallback(() => Hide())
            .Build();
        MainCore.TC.Play(autoHideSeq);
    }
    private static void Hide() {
        if(!visible) return;
        visible = false;
        autoHideSeq?.Kill();
        moveSeq?.Kill();
        moveSeq = GTweenSequenceBuilder.New()
            .Join(toastRect.GTAnchorPos(HiddenPos, 0.35f).SetEasing(Easing.OutExpo))
            .Join(group.GTFade(0f, 0.25f).SetEasing(Easing.OutSine))
            .AppendCallback(() => {
                if(!visible && toastObj != null) toastObj.SetActive(false);
            })
            .Build();
        MainCore.TC.Play(moveSeq);
    }
    private static void Dismiss() {
        dismissedTag = shownTag;
        Hide();
    }
    private static void OpenSettings() {
        Hide();
        if(UICore.IsReorganizing) UICore.ExitReorganize();
        UICore.Open();
        MenuFactory.SetState((int)OriginalMenuState.Settings);
        PageSettings.ScrollToUpdates();
    }
    private static void RenderText() {
        if(titleText == null) return;
        titleText.text = $"{MainCore.Tr.Get("UPDATE_AVAILABLE", "Update available:")} {shownTag}";
        hintText.text = string.IsNullOrEmpty(shownName)
            ? MainCore.Tr.Get("UPDATE_TOAST_HINT", "Click to open Settings")
            : shownName;
    }
    public static void Dispose() {
        UpdateService.OnChanged -= Refresh;
        if(_onLanguageChanged != null) {
            MainCore.Tr.OnLanguageChanged -= _onLanguageChanged;
            _onLanguageChanged = null;
        }
        moveSeq?.Kill();
        hoverSeq?.Kill();
        autoHideSeq?.Kill();
        if(canvasObj != null) {
            UnityEngine.Object.Destroy(canvasObj);
            canvasObj = null;
        }
        toastObj = null;
        toastRect = null;
        group = null;
        bg = null;
        titleText = null;
        hintText = null;
        visible = false;
        shownTag = null;
        shownName = null;
    }
}
