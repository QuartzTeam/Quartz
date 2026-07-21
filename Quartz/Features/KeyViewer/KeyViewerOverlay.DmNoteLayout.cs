using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static void AddDmNoteBox(int index, DmNoteSpec spec) {
        if(spec.IsGraph) {
            AddDmNoteGraph(index, spec);
            return;
        }
        (Image fill, Image border) = NewBoxVisual(
            "DmNote_" + index, root, spec.X, spec.Y, spec.W, spec.H,
            spec.BorderRadius, spec.BoxBorderWidth
        );
        Box box = new() {
            Key = spec.KeyCode,
            Name = spec.CountKey,
            Fill = fill,
            Border = border,
            Dm = spec,
            Source = spec.Source,
            CountInTotal = spec.CountInTotal,
            PerKeyKps = spec.PerKeyKps,
            IsKps = spec.IsKps,
            IsKpsAvg = spec.IsKpsAvg,
            IsKpsMax = spec.IsKpsMax,
            IsTotal = spec.IsTotal,
            Count = spec.IsStat ? 0
                : spec.Source != null ? spec.Source.Count
                : Conf.GetCount(spec.CountKey),
            RainGroup = 1,
            CenterX = spec.TrackX + spec.NoteW * 0.5f,
            BoxW = spec.NoteW,
        };
        if(spec.LabelEnabled) {
            box.Label = NewText(fill.transform, "Label", spec.DisplayText, spec.FontSize);
            box.Label.enableAutoSizing = true;
            box.Label.fontSizeMin = 0f;
            box.Label.fontSizeMax = Mathf.Max(8, spec.FontSize);
            if(spec.LabelFontStyles != FontStyles.Normal) box.Label.fontStyle |= spec.LabelFontStyles;
            if(spec.InlineStatCounter) {
                box.Label.text = DmInlineStatText(spec, spec.IsTotal ? totalCount : 0);
                box.DmStatPrefix = ((spec.DisplayText ?? "") + "  ").ToCharArray();
                LayoutDmText(box.Label.rectTransform, spec, false);
                box.Label.alignment = TextAlignmentOptions.Center;
            } else {
                LayoutDmText(box.Label.rectTransform, spec, false);
                box.Label.alignment = DmCounterAlignment(spec, false);
            }
        }
        if(spec.CounterEnabled && !spec.InlineStatCounter) {
            Transform counterParent = spec.CounterOutside ? root : fill.transform;
            box.Value = NewText(counterParent, "Counter", "0", spec.CounterFontSize);
            box.Value.enableAutoSizing = true;
            box.Value.fontSizeMin = 0f;
            box.Value.fontSizeMax = Mathf.Max(8, spec.CounterFontSize);
            if(spec.CounterFontStyles != FontStyles.Normal) box.Value.fontStyle |= spec.CounterFontStyles;
            if(spec.CounterOutside) {
                LayoutDmOutsideCounter(box.Value.rectTransform, spec);
                box.Value.alignment = TextAlignmentOptions.Center;
            } else {
                LayoutDmText(box.Value.rectTransform, spec, true);
                box.Value.alignment = DmCounterAlignment(spec, true);
            }
        }
        boxes.Add(box);
        BuildCssFx(box, spec);
        ApplyBoxColors(box);
    }
    private const float LineHeight = 1.2f;
    internal static void LayoutDmText(RectTransform rt, DmNoteSpec spec, bool counter, bool counterHidden = false) {
        bool top = string.Equals(spec.CounterAlign, "top", StringComparison.OrdinalIgnoreCase);
        bool bottom = string.Equals(spec.CounterAlign, "bottom", StringComparison.OrdinalIgnoreCase);
        bool left = string.Equals(spec.CounterAlign, "left", StringComparison.OrdinalIgnoreCase);
        bool right = string.Equals(spec.CounterAlign, "right", StringComparison.OrdinalIgnoreCase);
        bool between = string.Equals(spec.CounterAlignMode, "between", StringComparison.OrdinalIgnoreCase);
        float gap = Mathf.Clamp(spec.CounterGap, -64f, 64f);
        if(!spec.CounterEnabled || counterHidden || spec.CounterOutside || spec.InlineStatCounter || string.IsNullOrWhiteSpace(spec.DisplayText)) {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(spec.W - 4f, spec.H - 4f);
            return;
        }
        if(top || bottom) {
            float itemGap = between ? 0f : Mathf.Max(0f, gap);
            float avail = Mathf.Max(1f, spec.H - 4f);
            float labelH = Mathf.Clamp(spec.FontSize * LineHeight, 1f, avail);
            float counterH = Mathf.Clamp(spec.CounterFontSize * LineHeight, 1f, avail);
            if(labelH + counterH + itemGap > avail) {
                float k = Mathf.Max(1f, avail - itemGap) / (labelH + counterH);
                labelH *= k;
                counterH *= k;
            }
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            float groupH = Mathf.Min(avail, labelH + counterH + itemGap);
            float y0 = Mathf.Max(2f, (spec.H - groupH) * 0.5f);
            rt.anchoredPosition = counter == bottom
                ? new Vector2(2f, y0)
                : new Vector2(2f, y0 + counterH + itemGap);
            if(top)
                rt.anchoredPosition = counter
                    ? new Vector2(2f, y0 + labelH + itemGap)
                    : new Vector2(2f, y0);
            rt.sizeDelta = new Vector2(spec.W - 4f, counter ? counterH : labelH);
            return;
        }
        if(left || right) {
            float itemGap = between ? 0f : Mathf.Max(0f, gap);
            float availW = Mathf.Max(1f, spec.W - 4f);
            float labelW = Mathf.Clamp(spec.FontSize * Mathf.Max(1f, (spec.DisplayText ?? "").Length) * 0.58f + 4f, 1f, availW);
            float counterW = Mathf.Clamp(spec.CounterFontSize * 3f, 1f, availW);
            if(labelW + counterW + itemGap > availW) {
                float k = Mathf.Max(1f, availW - itemGap) / (labelW + counterW);
                labelW *= k;
                counterW *= k;
            }
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            float groupW = Mathf.Min(availW, labelW + counterW + itemGap);
            float x0 = Mathf.Max(2f, (spec.W - groupW) * 0.5f);
            rt.anchoredPosition = counter == left
                ? new Vector2(x0, 2f)
                : new Vector2(x0 + counterW + itemGap, 2f);
            if(right)
                rt.anchoredPosition = counter
                    ? new Vector2(x0 + labelW + itemGap, 2f)
                    : new Vector2(x0, 2f);
            rt.sizeDelta = new Vector2(counter ? counterW : labelW, spec.H - 4f);
            return;
        }
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(spec.W - 4f, spec.H - 4f);
    }
    internal static void LayoutDmOutsideCounter(RectTransform rt, DmNoteSpec spec) {
        string align = spec.CounterAlign;
        float gap = Mathf.Max(0f, spec.CounterGap);
        float w = Mathf.Max(spec.W, spec.CounterFontSize * 4f);
        float h = Mathf.Max(12f, spec.CounterFontSize + 8f);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        if(string.Equals(align, "bottom", StringComparison.OrdinalIgnoreCase)) {
            rt.anchoredPosition = new Vector2(spec.X + spec.W * 0.5f - w * 0.5f, -(spec.Y + spec.H + gap));
            rt.sizeDelta = new Vector2(w, h);
            return;
        }
        if(string.Equals(align, "left", StringComparison.OrdinalIgnoreCase)) {
            rt.anchoredPosition = new Vector2(spec.X - gap - w, -(spec.Y + spec.H * 0.5f - h * 0.5f));
            rt.sizeDelta = new Vector2(w, h);
            return;
        }
        if(string.Equals(align, "right", StringComparison.OrdinalIgnoreCase)) {
            rt.anchoredPosition = new Vector2(spec.X + spec.W + gap, -(spec.Y + spec.H * 0.5f - h * 0.5f));
            rt.sizeDelta = new Vector2(w, h);
            return;
        }
        rt.anchoredPosition = new Vector2(spec.X + spec.W * 0.5f - w * 0.5f, -(spec.Y - gap - h));
        rt.sizeDelta = new Vector2(w, h);
    }
    internal static TextAlignmentOptions DmCounterAlignment(DmNoteSpec spec, bool counter) {
        string align = spec.CounterAlign;
        bool between = string.Equals(spec.CounterAlignMode, "between", StringComparison.OrdinalIgnoreCase);
        if(!between && (string.Equals(align, "top", StringComparison.OrdinalIgnoreCase)
            || string.Equals(align, "bottom", StringComparison.OrdinalIgnoreCase))) {
            return TextAlignmentOptions.Center;
        }
        if(string.Equals(align, "top", StringComparison.OrdinalIgnoreCase)) return counter ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Top;
        if(string.Equals(align, "bottom", StringComparison.OrdinalIgnoreCase)) return counter ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom;
        if(!between && (string.Equals(align, "left", StringComparison.OrdinalIgnoreCase)
            || string.Equals(align, "right", StringComparison.OrdinalIgnoreCase))) {
            return TextAlignmentOptions.Center;
        }
        if(string.Equals(align, "left", StringComparison.OrdinalIgnoreCase)) return counter ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        if(string.Equals(align, "right", StringComparison.OrdinalIgnoreCase)) return counter ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        return TextAlignmentOptions.Center;
    }
    private static string DmInlineStatText(DmNoteSpec spec, int value)
        => (spec.DisplayText ?? "") + "  " + value.ToString(CultureInfo.InvariantCulture);
}
