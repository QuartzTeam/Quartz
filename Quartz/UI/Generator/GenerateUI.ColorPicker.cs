using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;

using TMPro;

namespace Quartz.UI.Generator;

// HSV + RGBA color picker builder. Grafted from the KorenResourcePack fork —
// the one widget upstream Overlayer doesn't ship. Builds the collapsed header
// (label/value/swatch) plus the expandable body (SV square, hue bar, hex input,
// R/G/B/A channel sliders) and wires pointer drag handling to the UIColorPicker.
public static partial class GenerateUI {
    public static UIColorPicker ColorPicker(
        Transform parent,
        Color defaultValue,
        Color value,
        Action<Color> onChanged,
        Action<Color> onComplete,
        string text,
        string id,
        bool showAlpha = true
    ) {
        // The R/G/B/A channel sliders stack full-width below the SV/hue band as
        // standard 50px slider rows. Body sizing follows the last one so the
        // rounded background always wraps it (no overflow).
        const float sliderTop = 212f;
        const float sliderStep = 58f;
        const float sliderHeight = 50f;
        int sliderCount = showAlpha ? 4 : 3;
        float lastSliderBottom = sliderTop + (sliderCount - 1) * sliderStep + sliderHeight;
        float bodyHeight = Mathf.Max(200f, lastSliderBottom) + 14f;
        float expandedHeight = 62f + bodyHeight;

        GameObject root = new("ColorPicker");
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        RectTransform header = BackGround();
        header.SetParent(root.transform, false);
        header.anchorMin = new(0f, 1f);
        header.anchorMax = new(1f, 1f);
        header.pivot = new(0.5f, 1f);
        header.offsetMin = new(0f, -50f);
        header.offsetMax = new(-250f, 0f);

        TextMeshProUGUI label = AddText(header);
        label.text = text;
        LocalizeById(label, id, text);

        TextMeshProUGUI valueText = AddText(header);
        valueText.alignment = TextAlignmentOptions.Right;
        RectTransform valueRect = valueText.rectTransform;
        valueRect.offsetMin = new(0f, 0f);
        valueRect.offsetMax = new(-72f, 0f);

        GameObject changed = AddSmallChangedCircle(header);
        Image changedImg = changed.GetComponent<Image>();

        GameObject swatch = new("Swatch");
        swatch.transform.SetParent(header, false);
        RectTransform swatchRect = swatch.AddComponent<RectTransform>();
        swatchRect.anchorMin = new(1f, 0.5f);
        swatchRect.anchorMax = new(1f, 0.5f);
        swatchRect.pivot = new(0.5f, 0.5f);
        swatchRect.anchoredPosition = new(-30f, 0f);
        swatchRect.sizeDelta = new(32f, 32f);
        Image swatchImg = swatch.AddComponent<Image>();
        swatchImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        swatchImg.type = Image.Type.Sliced;

        GameObject body = new("PickerBody");
        body.transform.SetParent(root.transform, false);
        RectTransform bodyRect = body.AddComponent<RectTransform>();
        bodyRect.anchorMin = new(0f, 1f);
        bodyRect.anchorMax = new(1f, 1f);
        bodyRect.pivot = new(0.5f, 1f);
        bodyRect.offsetMin = new(0f, -(62f + bodyHeight));
        bodyRect.offsetMax = new(-250f, -62f);
        Image bodyBg = body.AddComponent<Image>();
        bodyBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        bodyBg.type = Image.Type.Sliced;
        // Slightly darker than ObjectBG so the channel sliders (standard
        // ObjectBG bars) sit on it with the same contrast they get on a page.
        bodyBg.color = Color.Lerp(UIColors.ObjectBG, Color.black, 0.18f);

        // Fades the body in/out during the expand animation.
        CanvasGroup bodyCg = body.AddComponent<CanvasGroup>();

        GameObject sv = new("SaturationValue");
        sv.transform.SetParent(body.transform, false);
        RectTransform svRect = sv.AddComponent<RectTransform>();
        svRect.anchorMin = new(0f, 1f);
        svRect.anchorMax = new(0f, 1f);
        svRect.pivot = new(0f, 1f);
        svRect.anchoredPosition = new(16f, -12f);
        svRect.sizeDelta = new(188f, 188f);
        RawImage svImage = sv.AddComponent<RawImage>();

        GameObject svHandle = new("Handle");
        svHandle.transform.SetParent(sv.transform, false);
        RectTransform svHandleRect = svHandle.AddComponent<RectTransform>();
        svHandleRect.anchorMin = new(0f, 1f);
        svHandleRect.anchorMax = new(0f, 1f);
        svHandleRect.pivot = new(0.5f, 0.5f);
        svHandleRect.sizeDelta = new(18f, 18f);
        Image svHandleImg = svHandle.AddComponent<Image>();
        svHandleImg.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        svHandleImg.type = Image.Type.Sliced;
        svHandleImg.color = Color.white;
        svHandleImg.raycastTarget = false;

        GameObject hue = new("Hue");
        hue.transform.SetParent(body.transform, false);
        RectTransform hueRect = hue.AddComponent<RectTransform>();
        hueRect.anchorMin = new(0f, 1f);
        hueRect.anchorMax = new(0f, 1f);
        hueRect.pivot = new(0f, 1f);
        hueRect.anchoredPosition = new(216f, -12f);
        hueRect.sizeDelta = new(28f, 188f);
        RawImage hueImage = hue.AddComponent<RawImage>();

        GameObject hueHandle = new("Handle");
        hueHandle.transform.SetParent(hue.transform, false);
        RectTransform hueHandleRect = hueHandle.AddComponent<RectTransform>();
        hueHandleRect.anchorMin = new(0.5f, 1f);
        hueHandleRect.anchorMax = new(0.5f, 1f);
        hueHandleRect.pivot = new(0.5f, 0.5f);
        hueHandleRect.sizeDelta = new(36f, 5f);
        Image hueHandleImg = hueHandle.AddComponent<Image>();
        hueHandleImg.color = Color.white;
        hueHandleImg.raycastTarget = false;

        GameObject preview = new("Preview");
        preview.transform.SetParent(body.transform, false);
        RectTransform previewRect = preview.AddComponent<RectTransform>();
        previewRect.anchorMin = new(0f, 1f);
        previewRect.anchorMax = new(0f, 1f);
        previewRect.pivot = new(0f, 1f);
        previewRect.anchoredPosition = new(264f, -18f);
        previewRect.sizeDelta = new(58f, 58f);
        Image previewImg = preview.AddComponent<Image>();
        previewImg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        previewImg.type = Image.Type.Sliced;

        GameObject hexObj = new("Hex");
        hexObj.transform.SetParent(body.transform, false);
        RectTransform hexRect = hexObj.AddComponent<RectTransform>();
        hexRect.anchorMin = new(0f, 1f);
        hexRect.anchorMax = new(1f, 1f);
        hexRect.pivot = new(0f, 1f);
        hexRect.offsetMin = new(334f, -58f);
        hexRect.offsetMax = new(-18f, -18f);
        Image hexBg = hexObj.AddComponent<Image>();
        hexBg.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P2048);
        hexBg.type = Image.Type.Sliced;
        hexBg.color = Color.Lerp(UIColors.ObjectBG, Color.black, 0.15f);
        hexObj.AddComponent<RectMask2D>();

        TMP_InputField hexInput = hexObj.AddComponent<TMP_InputField>();

        GameObject hexTextObj = new("Text");
        hexTextObj.transform.SetParent(hexObj.transform, false);
        RectTransform hexTextRect = hexTextObj.AddComponent<RectTransform>();
        hexTextRect.anchorMin = Vector2.zero;
        hexTextRect.anchorMax = Vector2.one;
        hexTextRect.offsetMin = new(12f, 0f);
        hexTextRect.offsetMax = new(-8f, 0f);

        TextMeshProUGUI hexText = hexTextObj.AddComponent<TextMeshProUGUI>();
        hexText.font = FontManager.Current;
        hexText.fontSize = 22f;
        hexText.color = Color.white;
        hexText.alignment = TextAlignmentOptions.Left;
        hexText.verticalAlignment = VerticalAlignmentOptions.Middle;
        hexText.characterSpacing = -3f;
        hexText.textWrappingMode = TextWrappingModes.NoWrap;

        hexInput.textViewport = hexRect;
        hexInput.textComponent = hexText;

        // Forward-declared so each channel slider's callbacks can reach the
        // picker; it's assigned just below, before any callback can fire.
        UIColorPicker picker = null;

        UIColorPicker.ChannelSlider CreateChannelSlider(string channelLabel, int channel, float top) {
            float component = channel switch {
                0 => value.r,
                1 => value.g,
                2 => value.b,
                _ => value.a,
            };
            float componentDefault = channel switch {
                0 => defaultValue.r,
                1 => defaultValue.g,
                2 => defaultValue.b,
                _ => defaultValue.a,
            };

            UISlider slider = Slider(
                body.transform,
                componentDefault, 0f, 1f, component,
                null,
                v => picker?.SetChannelValue(channel, v),
                _ => picker?.Commit(),
                channelLabel,
                id + "_ch" + channel
            );
            slider.Format = "0.00";

            // Pin as a full-width 50px row below the SV/hue band. Override the
            // bar's default -250 right reservation so it fills the picker body.
            RectTransform sr = slider.Rect;
            sr.anchorMin = new(0f, 1f);
            sr.anchorMax = new(1f, 1f);
            sr.pivot = new(0.5f, 1f);
            sr.offsetMin = new(16f, -top - sliderHeight);
            sr.offsetMax = new(-16f, -top);

            return new UIColorPicker.ChannelSlider(channel, slider);
        }

        UIColorPicker.ChannelSlider redSlider = CreateChannelSlider("R", 0, sliderTop);
        UIColorPicker.ChannelSlider greenSlider = CreateChannelSlider("G", 1, sliderTop + sliderStep);
        UIColorPicker.ChannelSlider blueSlider = CreateChannelSlider("B", 2, sliderTop + sliderStep * 2f);
        UIColorPicker.ChannelSlider alphaSlider = showAlpha ? CreateChannelSlider("A", 3, sliderTop + sliderStep * 3f) : null;

        UIColorPicker.ChannelSlider[] sliders = showAlpha
            ? new[] { redSlider, greenSlider, blueSlider, alphaSlider }
            : new[] { redSlider, greenSlider, blueSlider };

        picker = new UIColorPicker(
            id,
            rootRect,
            parent.GetComponent<LayoutElement>(),
            body,
            bodyCg,
            label,
            valueText,
            swatchImg,
            previewImg,
            changedImg,
            svRect,
            svImage,
            hueRect,
            hueImage,
            svHandleRect,
            hueHandleRect,
            hexInput,
            sliders,
            expandedHeight,
            defaultValue,
            value,
            onChanged,
            onComplete
        );

        AddButton(header.gameObject, btn => {
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

        void AddPickerDrag(RectTransform target, Action<Vector2> setFromPointer) {
            EventTrigger trigger = target.gameObject.AddComponent<EventTrigger>();

            UnityUtils.AddEvent(EventTriggerType.PointerDown, e => {
                PointerEventData p = e as PointerEventData;
                if(p == null || p.button != InputButton.Left) return;
                setFromPointer(p.position);
            }, trigger);

            UnityUtils.AddEvent(EventTriggerType.Drag, e => {
                PointerEventData p = e as PointerEventData;
                if(p == null || !UnityEngine.Input.GetMouseButton(0)) return;
                setFromPointer(p.position);
            }, trigger);

            UnityUtils.AddEvent(EventTriggerType.PointerUp, e => {
                PointerEventData p = e as PointerEventData;
                if(p == null || p.button != InputButton.Left) return;
                picker.Commit();
            }, trigger);
        }

        AddPickerDrag(svRect, picker.SetFromSvPointer);
        AddPickerDrag(hueRect, picker.SetFromHuePointer);

        return picker;
    }
}
