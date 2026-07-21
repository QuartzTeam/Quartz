using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Resource;
using Quartz.Tween;
using Quartz.UI;
using Quartz.UI.Generator;
using Quartz.UI.Utility;
using GTweens.Tweens;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.UI.Factory.Page;
internal static partial class PagePanels {
    private sealed class StatColorBody {
        public RectTransform Rect;
        public VerticalLayoutGroup Layout;
        public ContentSizeFitter Fitter;
        public LayoutElement LE;
        public CanvasGroup CG;
        public GTween Seq;
    }
    private static StatColorBody CreateColorBody(Transform parent) {
        GameObject obj = new("StatColorBody");
        obj.transform.SetParent(parent, false);
        StatColorBody body = new() {
            Rect = obj.AddComponent<RectTransform>(),
        };
        body.Layout = obj.AddComponent<VerticalLayoutGroup>();
        body.Layout.spacing = 6f;
        body.Layout.padding = new RectOffset(40, 0, 0, 6);
        body.Layout.childControlWidth = true;
        body.Layout.childControlHeight = true;
        body.Layout.childForceExpandWidth = true;
        body.Layout.childForceExpandHeight = false;
        body.Fitter = obj.AddComponent<ContentSizeFitter>();
        body.Fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        body.LE = obj.AddComponent<LayoutElement>();
        body.LE.preferredHeight = 0f;
        body.CG = obj.AddComponent<CanvasGroup>();
        body.CG.alpha = 0f;
        obj.AddComponent<RectMask2D>();
        return body;
    }
    private static string AnchorName(PanelAnchor anchor) => anchor switch {
        PanelAnchor.TopLeft => GenerateUI.Tr("ANCHOR_TOP_LEFT", "Top Left"),
        PanelAnchor.TopCenter => GenerateUI.Tr("ANCHOR_TOP_CENTER", "Top Center"),
        PanelAnchor.TopRight => GenerateUI.Tr("ANCHOR_TOP_RIGHT", "Top Right"),
        PanelAnchor.MiddleLeft => GenerateUI.Tr("ANCHOR_MIDDLE_LEFT", "Middle Left"),
        PanelAnchor.MiddleCenter => GenerateUI.Tr("ANCHOR_MIDDLE_CENTER", "Middle Center"),
        PanelAnchor.MiddleRight => GenerateUI.Tr("ANCHOR_MIDDLE_RIGHT", "Middle Right"),
        PanelAnchor.BottomLeft => GenerateUI.Tr("ANCHOR_BOTTOM_LEFT", "Bottom Left"),
        PanelAnchor.BottomCenter => GenerateUI.Tr("ANCHOR_BOTTOM_CENTER", "Bottom Center"),
        PanelAnchor.BottomRight => GenerateUI.Tr("ANCHOR_BOTTOM_RIGHT", "Bottom Right"),
        _ => anchor.ToString(),
    };
    private static string StatDefaultLabel(string id) {
        foreach(PanelsOverlay.StatDef stat in PanelsOverlay.AllStats)
            if(stat.Id == id) return stat.Label;
        return id;
    }
    private static void BuildTextEntryInput(Transform bg, StatEntry entry, Action save) {
        GameObject inputObj = new("TextInput");
        inputObj.transform.SetParent(bg, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.offsetMin = new Vector2(48f, 6f);
        inputRect.offsetMax = new Vector2(-300f, -6f);
        Image fieldBg = inputObj.AddComponent<Image>();
        fieldBg.color = UIColors.ObjectBG;
        fieldBg.raycastTarget = true;
        inputObj.AddComponent<RectMask2D>();
        TMP_InputField field = inputObj.AddComponent<TMP_InputField>();
        var text = GenerateUI.AddText(inputObj.transform, true);
        text.alignment = TextAlignmentOptions.Left;
        TextCompat.NoWrap(text);
        SetFullRect(text.rectTransform, 10f);
        var placeholder = GenerateUI.AddText(inputObj.transform, true);
        placeholder.alignment = TextAlignmentOptions.Left;
        TextCompat.NoWrap(placeholder);
        placeholder.color = new Color(1f, 1f, 1f, 0.3f);
        GenerateUI.Localize(placeholder, "PANEL_TEXT_PLACEHOLDER", "Custom text…");
        SetFullRect(placeholder.rectTransform, 10f);
        field.textViewport = inputRect;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false;
        field.characterLimit = 64;
        field.SetTextWithoutNotify(entry.Text ?? "");
        field.onValueChanged.AddListener(v => entry.Text = v);
        field.onEndEdit.AddListener(_ => save());
    }
    private static void SetFullRect(RectTransform rect, float xPad) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(xPad, 0f);
        rect.offsetMax = new Vector2(-xPad, 0f);
    }
    private static void AddPanelLayerHandle(GenerateUI.CollapsibleSection sec, PanelConfig panel) {
        if(sec.HeaderObj.transform.Find("Bar") is RectTransform barRect)
            barRect.offsetMin = new Vector2(44f, barRect.offsetMin.y);
        GameObject handle = MakeDragHandle(sec.HeaderObj.transform, "LayerHandle", 44f);
        PanelLayerDrag drag = handle.AddComponent<PanelLayerDrag>();
        drag.Row = sec.Section;
        handle.transform.AddToolTip(
            "DESC_PANEL_LAYER",
            "Drag to reorder. Panels higher in the list draw on top where they overlap."
        );
    }
    private static void CommitPanelOrder() {
        if(panelsList == null) return;
        List<PanelConfig> order = [];
        for(int i = 0; i < panelsList.transform.childCount; i++) {
            PanelSectionMarker marker =
                panelsList.transform.GetChild(i).GetComponent<PanelSectionMarker>();
            if(marker != null && marker.Config != null) order.Add(marker.Config);
        }
        if(order.Count == 0) return;
        PanelsOverlay.Conf.Panels.Clear();
        PanelsOverlay.Conf.Panels.AddRange(order);
        PanelsOverlay.Save();
        PanelsOverlay.Rebuild();
    }
}
