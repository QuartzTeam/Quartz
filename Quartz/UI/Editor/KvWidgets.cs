using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal static partial class KvWidgets {
    internal const float MinPaneWidth = 260f;
    internal static float DefaultPaneWidth => KvPalette.PanelWidth;
    internal const float RowHeight = 50f;
    private const float Pad = 12f;
    private const float LabelSize = 19f;
    private const float LabelSizeMin = 15f;
    private static void Fit(TextMeshProUGUI tmp, float max, float min) {
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = min;
        tmp.fontSizeMax = max;
    }
    private const float SwitchReserve = 48f;
    private const float ValueReserve = 104f;
    private static TextMeshProUGUI Caption(
        RectTransform parent, string text, string id, float rightReserve, string labelKey = null
    ) {
        TextMeshProUGUI tmp = GenerateUI.AddText(parent, true);
        tmp.fontSize = LabelSize;
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        TextCompat.NoWrap(tmp);
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        Fit(tmp, LabelSize, LabelSizeMin);
        if(labelKey != null) GenerateUI.Localize(tmp, labelKey, text);
        else GenerateUI.LocalizeById(tmp, id, text);
        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Pad, 0f);
        rect.offsetMax = new Vector2(-rightReserve, 0f);
        return tmp;
    }
    internal static UIToggle Toggle(
        Transform parent, bool defaultValue, bool value, Action<bool> onChanged, string text, string id
    ) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        TextMeshProUGUI label = Caption(rect, text, id, SwitchReserve);
        Image changedImg = GenerateUI.AddSmallChangedCircle(rect).GetComponent<Image>();
        GameObject circle = new("ToggleCircle");
        circle.transform.SetParent(rect, false);
        RectTransform circleRect = circle.AddComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(1f, 0.5f);
        circleRect.anchorMax = new Vector2(1f, 0.5f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.anchoredPosition = new Vector2(-23f, 0f);
        circleRect.sizeDelta = new Vector2(26f, 26f);
        Image circleImage = circle.AddComponent<Image>();
        UIToggle toggle = new(id, rect, label, circleImage, circleRect, changedImg, defaultValue, value, onChanged);
        GenerateUI.AddButton(rect.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    toggle.Toggle();
                    break;
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault && toggle.Value != toggle.DefaultValue) toggle.Reset();
                    break;
            }
        });
        return toggle;
    }
    internal static UIButton Button(Transform parent, Action onClick, string text, string id) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        TextMeshProUGUI label = GenerateUI.AddText(rect, true);
        label.fontSize = LabelSize;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        TextCompat.NoWrap(label);
        label.overflowMode = TextOverflowModes.Ellipsis;
        Fit(label, LabelSize, LabelSizeMin);
        GenerateUI.LocalizeById(label, id, text);
        Image bg = rect.GetComponent<Image>();
        bg.color = UIColors.ObjectButton;
        UIButton button = new(id, rect, label, bg, onClick);
        GenerateUI.AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Left) button.Click();
        }, false);
        EventTrigger trigger = rect.gameObject.GetComponent<EventTrigger>()
            ?? rect.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerEnter, _ => button.OnHoverEnter(), trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerExit, _ => button.OnHoverExit(), trigger);
        return button;
    }
    internal static TextMeshProUGUI Header(RectTransform root, string key, string text) {
        RectTransform row = GenerateUI.Row(root, 30f);
        TextMeshProUGUI tmp = GenerateUI.AddText(row, true);
        tmp.fontSize = 21f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        TextCompat.NoWrap(tmp);
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        RectTransform rect = tmp.rectTransform;
        rect.offsetMin = new Vector2(Pad, 0f);
        rect.offsetMax = new Vector2(-Pad, 0f);
        return GenerateUI.Localize(tmp, key, text);
    }
    private const float SegmentLabelSize = 17f;
    private const float SegmentLabelSizeMin = 14f;
    internal static Action<T> Segments<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) {
        RectTransform row = GenerateUI.Row(root, 40f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        Action<T> setter = GenerateUI.SegmentedControl(row, values, name, key, value, onChanged);
        foreach(Transform child in row) {
            LayoutElement le = child.GetComponent<LayoutElement>();
            if(le == null) continue;
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.minWidth = 0f;
            TextMeshProUGUI label = child.GetComponentInChildren<TextMeshProUGUI>();
            if(label == null) continue;
            label.fontSize = SegmentLabelSize;
            TextCompat.NoWrap(label);
            label.overflowMode = TextOverflowModes.Ellipsis;
            Fit(label, SegmentLabelSize, SegmentLabelSizeMin);
        }
        return setter;
    }
}
