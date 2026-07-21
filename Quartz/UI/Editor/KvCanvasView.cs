using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private const string DefaultBg = "rgba(46, 46, 47, 0.9)";
    private const string DefaultBorder = "rgba(113, 113, 113, 0.9)";
    private const string DefaultFont = "rgba(121, 121, 121, 0.9)";
    private TextMeshProUGUI zoomLabel;
    private int shownZoom = -1;
    private Visual BuildVisual(KvElement el) {
        float radius = Mathf.Clamp(RawFloat(el, "borderRadius", 10f), 0f, 100f);
        float borderWidth = Mathf.Clamp(RawFloat(el, "borderWidth", 3f), 0f, 20f);
        (Image fill, Image border) = KeyViewerOverlay.NewBoxVisual(
            "Kv_" + el.Kind, content, el.X, el.Y, el.W, el.H, radius, borderWidth
        );
        Visual v = new() {
            El = el,
            Rect = fill.rectTransform,
            Fill = fill,
            Border = border,
        };
        bool stat = el.Kind != KvElementKind.Key;
        v.Label = KeyViewerOverlay.NewText(fill.transform, "Label", "", RawFloat(el, "fontSize", stat ? 16f : 18f));
        RectTransform labelRect = v.Label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        if(el.Kind is KvElementKind.Key or KvElementKind.Stat) {
            v.Counter = KeyViewerOverlay.NewText(fill.transform, "Counter", "0", 16f);
            v.Counter.raycastTarget = false;
        }
        GameObject outlineObj = new("Selected");
        outlineObj.transform.SetParent(fill.transform, false);
        RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = new Vector2(-3f, -3f);
        outlineRect.offsetMax = new Vector2(3f, 3f);
        v.Outline = outlineObj.AddComponent<Image>();
        v.Outline.sprite = MainCore.Spr.Get(UISliceSprite.CircleOutline256P2048);
        v.Outline.type = Image.Type.Sliced;
        v.Outline.color = UIColors.ObjectActive;
        v.Outline.raycastTarget = false;
        v.Outline.enabled = false;
        return v;
    }
    private void Paint(Visual v) {
        KvElement el = v.El;
        v.Rect.anchoredPosition = new Vector2(el.X, -el.Y);
        v.Rect.sizeDelta = new Vector2(el.W, el.H);
        ApplyShape(v, el);
        float dim = el.Hidden ? DimAlpha : 1f;
        v.Fill.color = Fade(KeyViewerOverlay.HexToColor(RawStr(el, "backgroundColor", DefaultBg), 0.9f), dim);
        v.Border.color = Fade(KeyViewerOverlay.HexToColor(RawStr(el, "borderColor", DefaultBorder), 0.9f), dim);
        KeyViewerOverlay.DmNoteSpec spec = CounterSpec(el);
        if(v.Label != null && v.Label.gameObject.activeSelf != el.LabelEnabled)
            v.Label.gameObject.SetActive(el.LabelEnabled);
        if(v.Label != null && el.LabelEnabled) {
            v.Label.color = Fade(KeyViewerOverlay.HexToColor(RawStr(el, "fontColor", DefaultFont), 1f), dim);
            v.Label.text = spec.InlineStatCounter ? LabelOf(el) + "  0" : LabelOf(el);
            KeyViewerOverlay.LayoutDmText(v.Label.rectTransform, spec, false);
            v.Label.alignment = spec.InlineStatCounter
                ? TextAlignmentOptions.Center
                : KeyViewerOverlay.DmCounterAlignment(spec, false);
        }
        PaintCounter(v, el, spec, dim);
        if(v.Outline != null) v.Outline.enabled = selection.Contains(el);
    }
    private void PaintCounter(Visual v, KvElement el, KeyViewerOverlay.DmNoteSpec spec, float dim) {
        if(v.Counter == null) return;
        bool show = spec.CounterEnabled && !spec.InlineStatCounter;
        v.Counter.gameObject.SetActive(show);
        if(!show) return;
        Transform parent = spec.CounterOutside ? content : v.Fill.transform;
        if(v.Counter.transform.parent != parent) v.Counter.transform.SetParent(parent, false);
        if(spec.CounterOutside) {
            KeyViewerOverlay.LayoutDmOutsideCounter(v.Counter.rectTransform, spec);
            v.Counter.alignment = TextAlignmentOptions.Center;
        } else {
            KeyViewerOverlay.LayoutDmText(v.Counter.rectTransform, spec, true);
            v.Counter.alignment = KeyViewerOverlay.DmCounterAlignment(spec, true);
        }
        v.Counter.fontSize = spec.CounterFontSize;
        v.Counter.color = Fade(CounterColor(el), dim);
    }
    private static Color CounterColor(KvElement el) {
        string fontHex = RawStr(el, "fontColor", DefaultFont);
        string idle = el.Raw["counter"]?["fill"]?["idle"]?.ToString();
        return KeyViewerOverlay.HexToColor(string.IsNullOrEmpty(idle) ? fontHex : idle, 1f);
    }
    private static KeyViewerOverlay.DmNoteSpec CounterSpec(KvElement el) {
        JObject c = el.Raw["counter"] as JObject;
        bool stat = el.Kind == KvElementKind.Stat;
        KeyViewerOverlay.DmNoteSpec spec = new() {
            X = el.X,
            Y = el.Y,
            W = el.W,
            H = el.H,
            DisplayText = el.LabelEnabled ? LabelOf(el) : "",
            FontSize = Mathf.RoundToInt(RawFloat(el, "fontSize", stat ? 16f : 18f)),
            CounterEnabled = (el.Kind == KvElementKind.Key || stat)
                && (c?["enabled"] is not JValue { Type: JTokenType.Boolean } enabled || enabled.ToObject<bool>()),
            CounterFontSize = c?["fontSize"] is JValue { Type: JTokenType.Integer or JTokenType.Float } size
                ? size.ToObject<int>() : 16,
            CounterAlign = c?["align"]?.ToString() is { Length: > 0 } align ? align : "top",
            CounterAlignMode = c?["alignMode"]?.ToString() is { Length: > 0 } mode ? mode : "center",
            CounterGap = c?["gap"] is JValue { Type: JTokenType.Integer or JTokenType.Float } gap
                ? gap.ToObject<float>() : 6f,
            CounterOutside = string.Equals(c?["placement"]?.ToString(), "outside", StringComparison.OrdinalIgnoreCase),
        };
        spec.InlineStatCounter = stat && spec.CounterEnabled && el.LabelEnabled
            && !spec.CounterOutside
            && string.Equals(spec.CounterAlignMode, "center", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(spec.CounterAlign, "top", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(spec.CounterAlign, "bottom", StringComparison.OrdinalIgnoreCase);
        return spec;
    }
    private static void ApplyShape(Visual v, KvElement el) {
        float radius = Mathf.Clamp(RawFloat(el, "borderRadius", 10f), 0f, 100f);
        float borderWidth = Mathf.Clamp(RawFloat(el, "borderWidth", 3f), 0f, 20f);
        if(v.Label != null) v.Label.fontSize = RawFloat(el, "fontSize", el.Kind == KvElementKind.Key ? 18f : 16f);
        if(Mathf.Approximately(radius, v.Radius) && Mathf.Approximately(borderWidth, v.BorderWidth)) return;
        v.Radius = radius;
        v.BorderWidth = borderWidth;
        v.Fill.pixelsPerUnitMultiplier = 8f / Mathf.Max(0.5f, radius);
        v.Border.sprite = MainCore.Spr.GetRing(Mathf.Max(0.5f, radius), Mathf.Max(0.1f, borderWidth));
    }
    private static Color Fade(Color c, float mul) {
        c.a *= mul;
        return c;
    }
    private static string LabelOf(KvElement el) {
        string text = el.DisplayText;
        if(!string.IsNullOrEmpty(text)) return text;
        return el.Kind switch {
            KvElementKind.Key => KeyViewerOverlay.KeyCodeShortLabel(el.KeyCodeValue),
            KvElementKind.Graph => "Graph",
            KvElementKind.Knob => "Knob",
            _ => string.IsNullOrEmpty(el.StatType) ? "Stat" : el.StatType,
        };
    }
    private static float RawFloat(KvElement el, string key, float fallback) {
        JToken t = el.Raw[key];
        if(t == null || t.Type == JTokenType.Null) return fallback;
        try {
            return t.ToObject<float>();
        } catch {
            return fallback;
        }
    }
    private static string RawStr(KvElement el, string key, string fallback) {
        JToken t = el.Raw[key];
        return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
    }
    private void BuildZoomLabel() {
        GameObject pillObj = new("ZoomIndicator");
        pillObj.transform.SetParent(viewport, false);
        RectTransform pill = pillObj.AddComponent<RectTransform>();
        pill.anchorMin = Vector2.zero;
        pill.anchorMax = Vector2.zero;
        pill.pivot = Vector2.zero;
        pill.anchoredPosition = new Vector2(8f, 8f);
        pill.sizeDelta = new Vector2(52f, 24f);
        Image bg = pillObj.AddComponent<Image>();
        bg.sprite = MainCore.Spr.GetFilled(4f);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        bg.raycastTarget = false;
        zoomLabel = KeyViewerOverlay.NewText(pill, "Zoom", "100%", 14f);
        RectTransform rect = zoomLabel.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        zoomLabel.alignment = TextAlignmentOptions.Center;
        zoomLabel.color = KvPalette.TextWhite;
        zoomLabel.raycastTarget = false;
    }
    private void SyncZoomLabel() {
        if(zoomLabel == null) return;
        int shown = Mathf.RoundToInt(zoom * 100f);
        if(shown == shownZoom) return;
        shownZoom = shown;
        zoomLabel.text = shown + "%";
    }
}
