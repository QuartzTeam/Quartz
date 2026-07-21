using Quartz.Core;
using Quartz.Localization;
using Quartz.Tween;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Builders;
using GTweens.Easings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal sealed class KvPopup : MonoBehaviour {
    private static float ItemHeight => 24f * KvPalette.Scale;
    private static float MinWidth => (108f + 10f) * KvPalette.Scale;
    private static float LabelSize => 13f * KvPalette.Scale;
    private static float ItemGap => 1f * KvPalette.Scale;
    private static float AnchorGap => 6f * KvPalette.Scale;
    private static KvPopup open;
    private Action onClosed;
    internal static void Show(
        RectTransform host, RectTransform anchor, IReadOnlyList<(string Key, string Text)> items,
        Action<int> onPick, Action onClosed = null
    ) {
        bool sameAnchor = open != null && open.transform.parent == host && ReferenceEquals(open.Anchor, anchor);
        CloseAny();
        if(sameAnchor) return;
        GameObject obj = new("KvPopup");
        obj.transform.SetParent(host, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        obj.AddComponent<LayoutElement>().ignoreLayout = true;
        obj.transform.SetAsLastSibling();
        KvPopup popup = obj.AddComponent<KvPopup>();
        popup.Anchor = anchor;
        popup.onClosed = onClosed;
        GameObject catcher = new("Catcher");
        catcher.transform.SetParent(rect, false);
        RectTransform catcherRect = catcher.AddComponent<RectTransform>();
        catcherRect.anchorMin = Vector2.zero;
        catcherRect.anchorMax = Vector2.one;
        catcherRect.offsetMin = Vector2.zero;
        catcherRect.offsetMax = Vector2.zero;
        catcher.AddComponent<EmptyGraphic>().raycastTarget = true;
        GenerateUI.AddButton(catcher, _ => CloseAny(), false);
        RectTransform tray = BuildTray(rect, items, onPick);
        Place(tray, anchor, rect);
        AnimateIn(tray);
        open = popup;
    }
    internal RectTransform Anchor { get; private set; }
    internal static void CloseAny() {
        if(open == null) return;
        KvPopup popup = open;
        open = null;
        Action closed = popup.onClosed;
        if(popup.gameObject != null) Destroy(popup.gameObject);
        closed?.Invoke();
    }
    private void OnDestroy() {
        if(open == this) open = null;
    }
    private static RectTransform BuildTray(
        RectTransform parent, IReadOnlyList<(string Key, string Text)> items, Action<int> onPick
    ) {
        GameObject obj = new("Tray");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.ButtonPrimary;
        VerticalLayoutGroup layout = obj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = ItemGap;
        int pad = Mathf.RoundToInt(KvPalette.PillPad);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = obj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for(int i = 0; i < items.Count; i++) {
            int index = i;
            Item(rect, items[i].Key, items[i].Text, () => {
                CloseAny();
                onPick?.Invoke(index);
            });
        }
        return rect;
    }
    private static void Item(RectTransform tray, string key, string text, Action onClick) {
        GameObject obj = new("Item");
        obj.transform.SetParent(tray, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(KvPalette.Radius);
        bg.type = Image.Type.Sliced;
        bg.color = KvPalette.ButtonPrimary;
        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minHeight = ItemHeight;
        le.preferredHeight = ItemHeight;
        le.minWidth = MinWidth;
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        label.text = text;
        label.color = KvPalette.TextDim;
        label.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(label);
        label.raycastTarget = false;
        label.gameObject.AddComponent<TextLocalization>().Init(key, text);
        GenerateUI.AddButton(obj, btn => {
            if(btn == InputButton.Left) onClick();
        }, false);
        EventTrigger trigger = obj.GetComponent<EventTrigger>() ?? obj.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => bg.color = KvPalette.ButtonHover, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => bg.color = KvPalette.ButtonPrimary, trigger);
    }
    private static void AnimateIn(RectTransform tray) {
        CanvasGroup cg = tray.GetComponent<CanvasGroup>() ?? tray.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        Vector2 target = tray.anchoredPosition;
        tray.anchoredPosition = target - new Vector2(0f, 8f * KvPalette.Scale);
        MainCore.TC.Play(GTweenSequenceBuilder.New()
            .Join(cg.GTFade(1f, 0.12f).SetEasing(Easing.OutSine))
            .Join(tray.GTAnchorPos(target, 0.17f).SetEasing(Easing.OutExpo))
            .Build());
    }
    private static void Place(RectTransform tray, RectTransform anchor, RectTransform host) {
        LayoutRebuilder.ForceRebuildLayoutImmediate(tray);
        tray.anchorMin = Vector2.zero;
        tray.anchorMax = Vector2.zero;
        tray.pivot = new Vector2(0.5f, 0f);
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 bottomLeft = host.InverseTransformPoint(corners[0]);
        Vector3 topRight = host.InverseTransformPoint(corners[2]);
        float centreX = (bottomLeft.x + topRight.x) * 0.5f;
        float half = tray.rect.width * 0.5f;
        Rect area = host.rect;
        float x = Mathf.Clamp(centreX, area.xMin + half + KvPalette.BarPad, area.xMax - half - KvPalette.BarPad);
        float y = topRight.y + AnchorGap;
        tray.anchoredPosition = new Vector2(x - area.xMin, y - area.yMin);
    }
}
