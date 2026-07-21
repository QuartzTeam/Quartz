using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal static partial class KvWidgets {
    private const float IconReserve = 44f;
    internal static UIInput Input(
        Transform parent,
        string defaultValue,
        string value,
        Action<string> onChanged,
        string placeholder,
        Sprite icon,
        string id
    ) {
        RectTransform rect = GenerateUI.BackGround(0f);
        rect.SetParent(parent, false);
        Image changedImg = GenerateUI.AddSmallChangedCircle(rect).GetComponent<Image>();
        GameObject iconObj = new("Icon");
        iconObj.transform.SetParent(rect, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-20f, 0f);
        iconRect.sizeDelta = new Vector2(22f, 22f);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = icon;
        iconImage.color = new Color(1f, 1f, 1f, 0.2f);
        iconImage.raycastTarget = false;
        GameObject inputObj = new("Input");
        inputObj.transform.SetParent(rect, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(Pad, 4f);
        inputRect.offsetMax = new Vector2(-IconReserve, -4f);
        inputObj.AddComponent<RectMask2D>();
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        TextMeshProUGUI text = FieldText(inputObj.transform, value ?? string.Empty, Color.white);
        TextMeshProUGUI placeholderText = FieldText(inputObj.transform, placeholder, new Color(1f, 1f, 1f, 0.2f));
        GenerateUI.LocalizeById(placeholderText, id, placeholder);
        inputField.textViewport = inputRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.richText = false;
        UIInput input = new(
            id, rect, inputField, placeholderText, iconImage, changedImg, defaultValue, value, onChanged
        );
        GenerateUI.AddButton(rect.gameObject, btn => {
            if(btn == InputButton.Middle
                && MainCore.Conf.MiddleClickToDefault
                && input.Value != input.DefaultValue) input.Reset();
        });
        return input;
    }
    private static TextMeshProUGUI FieldText(Transform parent, string value, Color color) {
        TextMeshProUGUI text = GenerateUI.AddText(parent, true);
        text.font = FontManager.Current;
        text.fontSize = LabelSize;
        text.text = value ?? string.Empty;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        TextCompat.NoWrap(text);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }
}
