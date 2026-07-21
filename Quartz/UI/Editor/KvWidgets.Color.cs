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
    private const float ColorHeaderH = RowHeight;
    private const float ColorHeaderGap = 6f;
    private const float SwatchSize = 28f;
    private const float HexH = 34f;
    private const float SvH = 110f;
    private const float HueW = 22f;
    private const float HueGap = 8f;
    private const float AlphaH = 34f;
    private const float ColorGapY = 10f;
    private const float ColorHeaderReserve = 120f;
    internal static UIColorPicker ColorPicker(
        Transform parent,
        Color defaultValue,
        Color value,
        Action<Color> onChanged,
        Action<Color> onComplete,
        string text,
        string id,
        bool showAlpha
    ) {
        float bodyHeight = (showAlpha ? 166f + ColorGapY + AlphaH : 166f) + 12f;
        float expandedHeight = ColorHeaderH + ColorHeaderGap + bodyHeight;
        GameObject root = new("KvColorPicker");
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        RectTransform header = GenerateUI.BackGround(0f);
        header.SetParent(root.transform, false);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(0f, -ColorHeaderH);
        header.offsetMax = Vector2.zero;
        TextMeshProUGUI label = Caption(header, text, id, ColorHeaderReserve);
        TextMeshProUGUI valueText = GenerateUI.AddText(header, true);
        valueText.fontSize = 17f;
        valueText.alignment = TextAlignmentOptions.Right;
        valueText.color = new Color(1f, 1f, 1f, 0.6f);
        TextCompat.NoWrap(valueText);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = new Vector2(-(Pad + SwatchSize + HueGap), 0f);
        Image changedImg = GenerateUI.AddSmallChangedCircle(header).GetComponent<Image>();
        GameObject swatch = new("Swatch");
        swatch.transform.SetParent(header, false);
        RectTransform swatchRect = swatch.AddComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(1f, 0.5f);
        swatchRect.anchorMax = new Vector2(1f, 0.5f);
        swatchRect.pivot = new Vector2(1f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(-Pad, 0f);
        swatchRect.sizeDelta = new Vector2(SwatchSize, SwatchSize);
        Image swatchImg = swatch.AddComponent<Image>();
        swatchImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        swatchImg.type = Image.Type.Sliced;
        GameObject body = new("PickerBody");
        body.transform.SetParent(root.transform, false);
        RectTransform bodyRect = body.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.offsetMin = new Vector2(0f, -(ColorHeaderH + ColorHeaderGap + bodyHeight));
        bodyRect.offsetMax = new Vector2(0f, -(ColorHeaderH + ColorHeaderGap));
        Image bodyBg = body.AddComponent<Image>();
        bodyBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bodyBg.type = Image.Type.Sliced;
        bodyBg.color = Color.Lerp(UIColors.ObjectBG, Color.black, 0.18f);
        CanvasGroup bodyCg = body.AddComponent<CanvasGroup>();
        TMP_InputField hexInput = HexField(body.transform);
        GameObject sv = new("SaturationValue");
        sv.transform.SetParent(body.transform, false);
        RectTransform svRect = sv.AddComponent<RectTransform>();
        svRect.anchorMin = new Vector2(0f, 1f);
        svRect.anchorMax = new Vector2(1f, 1f);
        svRect.pivot = new Vector2(0f, 1f);
        svRect.offsetMin = new Vector2(Pad, -(56f + SvH));
        svRect.offsetMax = new Vector2(-(Pad + HueW + HueGap), -56f);
        RawImage svImage = sv.AddComponent<RawImage>();
        RectTransform svHandleRect = Handle(sv.transform, new Vector2(0f, 1f), new Vector2(16f, 16f), true);
        GameObject hue = new("Hue");
        hue.transform.SetParent(body.transform, false);
        RectTransform hueRect = hue.AddComponent<RectTransform>();
        hueRect.anchorMin = new Vector2(1f, 1f);
        hueRect.anchorMax = new Vector2(1f, 1f);
        hueRect.pivot = new Vector2(1f, 1f);
        hueRect.anchoredPosition = new Vector2(-Pad, -56f);
        hueRect.sizeDelta = new Vector2(HueW, SvH);
        RawImage hueImage = hue.AddComponent<RawImage>();
        RectTransform hueHandleRect = Handle(hue.transform, new Vector2(0.5f, 1f), new Vector2(HueW + 8f, 4f), false);
        UIColorPicker picker = null;
        UIColorPicker.ChannelSlider[] sliders = [];
        if(showAlpha) {
            UISlider alpha = Slider(
                body.transform, defaultValue.a, 0f, 1f, value.a, null,
                v => picker?.SetChannelValue(3, v), _ => picker?.Commit(),
                "Alpha", id + "_alpha", "KVI_ALPHA"
            );
            alpha.Format = "0.00";
            RectTransform ar = alpha.Rect;
            ar.anchorMin = new Vector2(0f, 1f);
            ar.anchorMax = new Vector2(1f, 1f);
            ar.pivot = new Vector2(0.5f, 1f);
            ar.offsetMin = new Vector2(Pad, -(176f + AlphaH));
            ar.offsetMax = new Vector2(-Pad, -176f);
            sliders = [new UIColorPicker.ChannelSlider(3, alpha)];
        }
        picker = new UIColorPicker(
            id, rootRect, parent.GetComponent<LayoutElement>(), body, bodyCg,
            label, valueText, swatchImg, null, changedImg,
            svRect, svImage, hueRect, hueImage, svHandleRect, hueHandleRect,
            hexInput, sliders, expandedHeight, defaultValue, value, onChanged, onComplete
        );
        GenerateUI.AddButton(header.gameObject, btn => {
            switch(btn) {
                case InputButton.Left:
                    picker.ToggleExpanded();
                    break;
                case InputButton.Middle:
                    if(MainCore.Conf.MiddleClickToDefault) {
                        picker.Set(defaultValue);
                        picker.Commit();
                    }
                    break;
            }
        });
        PickerDrag(svRect, picker.SetFromSvPointer, picker);
        PickerDrag(hueRect, picker.SetFromHuePointer, picker);
        return picker;
    }
    private static RectTransform Handle(Transform parent, Vector2 anchor, Vector2 size, bool ring) {
        GameObject obj = new("Handle");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        if(ring) {
            img.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
            img.type = Image.Type.Sliced;
        }
        img.color = Color.white;
        img.raycastTarget = false;
        return rect;
    }
    private static TMP_InputField HexField(Transform parent) {
        GameObject obj = new("Hex");
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(Pad, -(Pad + HexH));
        rect.offsetMax = new Vector2(-Pad, -Pad);
        Image bg = obj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(UIColors.ObjectBG, Color.black, 0.15f);
        obj.AddComponent<RectMask2D>();
        TMP_InputField field = obj.AddComponent<TMP_InputField>();
        GameObject textObj = new("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.fontSize = 19f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.characterSpacing = -3f;
        TextCompat.NoWrap(text);
        field.textViewport = rect;
        field.textComponent = text;
        return field;
    }
    private static void PickerDrag(RectTransform target, Action<Vector2> setFromPointer, UIColorPicker picker) {
        EventTrigger trigger = target.gameObject.AddComponent<EventTrigger>();
        UnityUtils.AddEvent(EventTriggerType.PointerDown, e => {
            if(e.button == InputButton.Left) setFromPointer(e.position);
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.Drag, e => {
            if(UnityEngine.Input.GetMouseButton(0)) setFromPointer(e.position);
        }, trigger);
        UnityUtils.AddEvent(EventTriggerType.PointerUp, e => {
            if(e.button == InputButton.Left) picker.Commit();
        }, trigger);
    }
}
