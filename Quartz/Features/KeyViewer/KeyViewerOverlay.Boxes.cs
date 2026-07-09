using Quartz.Core;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Quartz.Features.KeyViewer;

// Box / stat / foot-key construction + label resolution + reorganize handle.
public static partial class KeyViewerOverlay {
    private static void AddKey(int[] keys, int slot, float x, float y, float w, float h) {
        if(slot < 0 || slot >= keys.Length) return;

        KeyCode key = (KeyCode)keys[slot];
        Box box = NewBox("Key_" + slot, x, y, w, h);
        box.Key = key;
        box.Slot = slot;
        int[] ghostKeys = Conf.GhostKeysForStyle(builtStyle);
        box.GhostKey = slot < ghostKeys.Length ? (KeyCode)ghostKeys[slot] : KeyCode.None;
        box.Name = key.ToString().ToUpperInvariant();
        box.Count = Conf.GetCount(box.Name);

        // v1 SlotRainGroup: front row = group 1, the 20-key style's third
        // row = group 3, everything else = group 2.
        box.RainGroup = slot < 8 ? 1 : builtStyle == 3 && slot >= 16 ? 3 : 2;
        box.CenterX = x + w * 0.5f;
        box.BoxW = w;

        // A wide key (e.g. the 10-key back row's 2-wide keys) pulls its one-key
        // rain toward the grid center: a key left of center aligns its rain to its
        // RIGHT edge, one right of center to its LEFT edge — so the two inner
        // rains sit next to each other instead of each emitting from the middle of
        // a wide key. Single keys (cols == 1) stay centered.
        int cols = Mathf.Max(1, Mathf.RoundToInt((w + KeyGap) / (KeyW + KeyGap)));
        if(cols > 1) {
            float gridCenter = SpanW(8) * 0.5f;
            box.RainAlign = box.CenterX < gridCenter - 0.5f ? 1f
                : box.CenterX > gridCenter + 0.5f ? -1f
                : 0f;
        }

        // With the counter shown, the label is lifted off the counter strip at
        // the bottom; with main counts hidden there's no strip, so the label
        // fills the box and reads vertically centered.
        bool showCount = !Conf.HideMainKeyCount;
        box.Label = NewText(box.Fill.transform, "Label", LabelFor(builtStyle, slot), KeyFontSize * Conf.KeyFontFor(slot));
        RectTransform labelRect = box.Label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(0f, showCount ? 12f : 0f);
        labelRect.offsetMax = Vector2.zero;

        // No counter object at all when main counts are hidden — keeps the label
        // centered and skips the per-frame counter work for these boxes.
        if(showCount) {
            box.Value = NewText(box.Fill.transform, "Counter", "0", CounterFontSize * Conf.CounterFontFor(slot));
            RectTransform counterRect = box.Value.rectTransform;
            counterRect.anchorMin = Vector2.zero;
            counterRect.anchorMax = new Vector2(1f, 0f);
            counterRect.pivot = new Vector2(0.5f, 0f);
            counterRect.anchoredPosition = new Vector2(0f, 3f);
            counterRect.sizeDelta = new Vector2(0f, 16f);
        }

        boxes.Add(box);
    }

    private static void AddStat(bool total, float x, float y, float w, float h) {
        Box box = NewBox(total ? "Total" : "Kps", x, y, w, h);
        box.IsKps = !total;
        box.IsTotal = total;
        string caption = total ? "Total" : "KPS";
        box.StatCaptionChars = (caption + "  ").ToCharArray();

        // 10/12-key styles (0/1): the stat box is narrow, so stack the caption
        // over the value instead of side by side. Overrides Together/Apart.
        bool stacked = builtStyle is 0 or 1;
        bool together = Conf != null && Conf.StatsTogether && !stacked;
        box.StatTogether = together;

        box.Label = NewText(box.Fill.transform, "Label", caption, StatFontSize);
        box.Value = NewText(box.Fill.transform, "Value", "0", StatFontSize);

        RectTransform labelRect = box.Label.rectTransform;
        RectTransform valueRect = box.Value.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;

        if(stacked) {
            // Caption in the top half, value in the bottom half (vertical stack).
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            box.Label.alignment = TextAlignmentOptions.Center;
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            box.Value.alignment = TextAlignmentOptions.Center;
        } else if(together) {
            // Caption + value centred together as one group ("KPS  0"). The
            // value text carries the caption (see the stat update in Update),
            // so the standalone label is hidden.
            box.Label.gameObject.SetActive(false);
            box.Value.alignment = TextAlignmentOptions.Center;
            box.Value.text = caption + "  0";
        } else {
            // Spread apart on one line: caption pinned left, value pinned right.
            labelRect.offsetMin = new Vector2(10f, 0f);
            box.Label.alignment = TextAlignmentOptions.MidlineLeft;
            valueRect.offsetMax = new Vector2(-10f, 0f);
            box.Value.alignment = TextAlignmentOptions.MidlineRight;
        }

        boxes.Add(box);
    }

    private static void AddFootKey(int footIndex, float x, float y, float w, float h) {
        int[] footKeys = Conf.FootKeys;
        if(footIndex < 0 || footIndex >= footKeys.Length) return;

        int slot = KeyViewerSettings.FootSlotBase + footIndex;
        KeyCode key = (KeyCode)footKeys[footIndex];
        // Foot boxes live under the separate foot element, not the main grid.
        (Image fill, Image border) = NewBoxVisual("Foot_" + footIndex, footRoot, x, y, w, h);
        Box box = new() { Border = border, Fill = fill };
        box.Key = key;
        box.Slot = slot;
        box.IsFoot = true;
        // Foot keys never rain or count; they only light on press.
        box.RainGroup = 0;
        box.CenterX = x + w * 0.5f;
        box.BoxW = w;
        box.Name = key.ToString().ToUpperInvariant();

        box.Label = NewText(box.Fill.transform, "Label", LabelFor(builtStyle, slot), FootFontSize * Conf.KeyFontFor(slot));
        RectTransform labelRect = box.Label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        boxes.Add(box);
    }

    private static void AddReorganizeHandle() =>
        dragObj = BuildReorganizeHandle(root, "Drag", "KEYVIEWER_TITLE", "Key Viewer");

    // Separate reorganize handle for the foot element, so it drags on its own.
    private static void AddFootReorganizeHandle() {
        if(footRoot == null) return;
        footDragObj = BuildReorganizeHandle(footRoot, "FootDrag", "KEYVIEWER_FOOT_TITLE", "Foot Keys");
    }

    // Full-rect drag surface parented to (and moving) target while reorganizing.
    private static GameObject BuildReorganizeHandle(RectTransform target, string name,
        string titleKey, string titleFallback) {
        GameObject drag = new(name);
        drag.transform.SetParent(target, false);
        RectTransform dragRect = drag.AddComponent<RectTransform>();
        dragRect.anchorMin = Vector2.zero;
        dragRect.anchorMax = Vector2.one;
        dragRect.offsetMin = Vector2.zero;
        dragRect.offsetMax = Vector2.zero;
        drag.AddComponent<EmptyGraphic>().raycastTarget = true;
        ReorganizeHandle handle = drag.AddComponent<ReorganizeHandle>();
        handle.Target = target;
        handle.GetName = () => MainCore.Tr.Get(titleKey, titleFallback);
        handle.OnMoved = Save;
        drag.SetActive(false);
        return drag;
    }

    // Box visuals: a rounded fill with an analytic ring drawn ON TOP of it —
    // a full border-colored rect behind a translucent fill would tint the
    // whole box with the outline color.
    internal static (Image fill, Image border) NewBoxVisual(
        string name, Transform parent, float x, float y, float w, float h,
        float radius = KeyRadius, float borderWidth = BorderWidth
    ) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);

        Image fill = obj.AddComponent<Image>();
        fill.sprite = MainCore.Spr.Get(UISliceSprite.Circle256P1024);
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 8f / Mathf.Max(0.5f, radius);
        fill.raycastTarget = false;

        GameObject borderObj = new("Border");
        borderObj.transform.SetParent(obj.transform, false);

        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        Image border = borderObj.AddComponent<Image>();
        border.sprite = MainCore.Spr.GetRing(Mathf.Max(0.5f, radius), Mathf.Max(0.1f, borderWidth));
        border.type = Image.Type.Sliced;
        border.raycastTarget = false;

        return (fill, border);
    }

    private static Box NewBox(string name, float x, float y, float w, float h) {
        (Image fill, Image border) = NewBoxVisual(name, root, x, y, w, h);
        return new Box { Border = border, Fill = fill };
    }

    // Caption for a key slot: the user override if set, else derived from the
    // bound key code. Shared with the settings-page preview.
    internal static string LabelFor(int style, int slot) {
        // Foot slots (20+) live in their own key/label arrays, shared by every
        // main style.
        if(slot >= KeyViewerSettings.FootSlotBase) {
            int fi = slot - KeyViewerSettings.FootSlotBase;
            string[] footOverrides = Conf.FootKeysText;
            if(fi >= 0 && fi < footOverrides.Length && !string.IsNullOrEmpty(footOverrides[fi])) return footOverrides[fi];
            int[] footKeys = Conf.FootKeys;
            return fi >= 0 && fi < footKeys.Length ? KeyCodeShortLabel((KeyCode)footKeys[fi]) : "";
        }

        string[] overrides = Conf.LabelsForStyle(style);
        if(slot >= 0 && slot < overrides.Length && !string.IsNullOrEmpty(overrides[slot])) return overrides[slot];

        int[] keys = Conf.KeysForStyle(style);
        return slot >= 0 && slot < keys.Length ? KeyCodeShortLabel((KeyCode)keys[slot]) : "";
    }

    internal static TextMeshProUGUI NewText(Transform parent, string name, string text, float fontSize) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.font = FontManager.Current;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }

    // v1 SimplePresets.KeyCodeShortLabel: compact key captions.
    internal static string KeyCodeShortLabel(KeyCode kc) {
        // Arrows resolve first: the Left/Right prefix rewrite below would turn
        // "LeftArrow"/"RightArrow" into "LArrow"/"RArrow", which then miss the
        // arrow-glyph switch at the end (Up/Down lack the prefix and worked).
        switch(kc) {
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
        }

        string s = kc.ToString();
        if(s.StartsWith("Alpha")) s = s[5..];
        // Numpad keys: "N" prefix + the symbol/digit (the generic transforms
        // below would otherwise leave "NMultiply", "NEnter" etc.).
        if(s.StartsWith("Keypad")) {
            string rest = s[6..];
            return "N" + rest switch {
                "Enter" => "↵",
                "Plus" => "+",
                "Minus" => "-",
                "Multiply" => "*",
                "Divide" => "/",
                "Period" => ".",
                "Equals" => "=",
                _ => rest,
            };
        }
        if(s.StartsWith("Left")) s = "L" + s[4..];
        if(s.StartsWith("Right")) s = "R" + s[5..];
        if(s.EndsWith("Shift")) s = s[..^5] + "⇧";
        if(s.EndsWith("Control")) s = s[..^7] + "Ctrl";
        return s switch {
            "PageUp" => "PgUp",
            "PageDown" => "PgDn",
            "Insert" => "Ins",
            "Delete" => "Del",
            "Numlock" => "NmLk",
            "ScrollLock" => "ScLk",
            "Print" or "SysReq" => "PrtSc",
            "Break" => "Brk",
            "Plus" => "+",
            "Minus" => "-",
            "Multiply" => "*",
            "Divide" => "/",
            "Enter" or "Return" => "↵",
            "Equals" => "=",
            "Period" => ".",
            "Comma" => ",",
            "Tab" => "⇥",
            "Space" => "␣",
            "Backslash" => "\\",
            "Slash" => "/",
            "Semicolon" => ";",
            "Quote" => "'",
            "BackQuote" => "`",
            "CapsLock" => "⇪",
            "Backspace" => "Back",
            "LBracket" or "LeftBracket" => "[",
            "RBracket" or "RightBracket" => "]",
            "None" => "",
            _ => s,
        };
    }
}