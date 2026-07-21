using System.Globalization;
using Quartz.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static List<DmNoteSpec> ParseDmNoteSpecs() {
        List<DmNoteSpec> result = [];
        dmCanvasHeight = 250f;
        dmCanvasWidth = 800f;
        ApplyDmRuntimeSettings();
        if(string.IsNullOrWhiteSpace(Conf.DmPresetJson)) return result;
        try {
            JObject preset = JObject.Parse(Conf.DmPresetJson);
            JObject keysTable = preset["keys"] as JObject;
            JObject posTable = (preset["keyPositions"] as JObject) ?? (preset["positions"] as JObject);
            string tab = ResolveDmTab(preset, keysTable, posTable);
            Conf.DmSelectedTab = tab;
            ApplyDmRuntimeSettings();
            JArray keyArr = keysTable?[tab] as JArray;
            JArray posArr = posTable?[tab] as JArray;
            if(keyArr == null || posArr == null) return result;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            int count = Mathf.Min(keyArr.Count, posArr.Count);
            for(int i = 0; i < count; i++) {
                if(posArr[i] is not JObject p || JBool(p, "hidden", false)) continue;
                DmNoteSpec spec = ParseDmNoteSpec(keyArr[i]?.ToString() ?? "", p, false);
                spec.ZIndex = JFloat(p, "zIndex", i);
                result.Add(spec);
                ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
            }
            if(preset["statPositions"] is JObject statTable && statTable[tab] is JArray statArr) {
                for(int i = 0; i < statArr.Count; i++) {
                    if(statArr[i] is not JObject p || JBool(p, "hidden", false)) continue;
                    JObject statPosition = (p["position"] as JObject) ?? p;
                    if(JBool(statPosition, "hidden", false)) continue;
                    DmNoteSpec spec = ParseDmNoteSpec(JStr(p, "statType", JStr(statPosition, "statType", "stat")), statPosition, true);
                    spec.ZIndex = JFloat(statPosition, "zIndex", i);
                    result.Add(spec);
                    ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            if(preset["graphPositions"] is JObject graphTable && graphTable[tab] is JArray graphArr) {
                for(int i = 0; i < graphArr.Count; i++) {
                    if(graphArr[i] is not JObject p || JBool(p, "hidden", false)) continue;
                    JObject pos = (p["position"] as JObject) ?? p;
                    if(JBool(pos, "hidden", false)) continue;
                    DmNoteSpec spec = ParseGraphSpec(pos);
                    spec.ZIndex = JFloat(pos, "zIndex", i);
                    result.Add(spec);
                    ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            FinishDmSpecs(result, minX, minY, maxX, maxY);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] DM Note parse failed: " + ex.Message);
            result.Clear();
        }
        ApplyCssToSpecs(result);
        return result;
    }
    private static void FinishDmSpecs(List<DmNoteSpec> specs, float minX, float minY, float maxX, float maxY) =>
        FinishDmSpecs(specs, minX, minY, maxX, maxY, (minX + maxX) * 0.5f, minY);
    private static void FinishDmSpecs(
        List<DmNoteSpec> specs, float minX, float minY, float maxX, float maxY,
        float anchorCx, float anchorMinY
    ) {
        if(float.IsPositiveInfinity(minX) || float.IsPositiveInfinity(minY)) return;
        const float padding = 30f;
        float track = Conf.DmNoteEffect ? dmTrackHeight : 0f;
        float topOffset = track + padding;
        dmCanvasWidth = Mathf.Max(60f, maxX - minX) + padding * 2f;
        dmCanvasHeight = Mathf.Max(60f, maxY - minY) + padding * 2f + track;
        float offsetX = dmCanvasWidth * 0.5f - anchorCx;
        float offsetY = topOffset - anchorMinY;
        for(int i = 0; i < specs.Count; i++) {
            DmNoteSpec spec = specs[i];
            spec.X += offsetX;
            spec.Y += offsetY;
            ResolveDmTrackGeometry(spec, topOffset);
        }
    }
    private static string ResolveDmTab(JObject preset, JObject keysTable, JObject posTable) {
        string selected = JOptionalString(preset, "selectedKeyType");
        if(!string.IsNullOrWhiteSpace(selected) && keysTable?[selected] != null && posTable?[selected] != null) return selected;
        string configured = Conf.DmSelectedTab;
        if(!string.IsNullOrWhiteSpace(configured) && keysTable?[configured] != null && posTable?[configured] != null) return configured;
        if(keysTable != null) {
            foreach(JProperty prop in keysTable.Properties())
                if(posTable?[prop.Name] != null) return prop.Name;
        }
        return string.IsNullOrWhiteSpace(configured) ? "4key" : configured;
    }
    private static void ApplyDmRuntimeSettings() {
        dmNoteSpeed = Mathf.Clamp(Conf.DmNoteSpeed, 1f, 5000f);
        dmTrackHeight = Mathf.Clamp(Conf.DmTrackHeight, 0f, 5000f);
        dmNoteReverse = Conf.DmNoteReverse;
        dmFadePx = Mathf.Clamp(Conf.DmFadePx, 0f, 2000f);
        dmDelayedNoteEnabled = Conf.DmDelayedNoteEnabled;
        dmShortNoteThresholdMs = Mathf.Clamp(Conf.DmShortNoteThresholdMs, 0f, 2000f);
        dmShortNoteMinLengthPx = Mathf.Clamp(Conf.DmShortNoteMinLengthPx, 1f, 9999f);
        dmKeyDisplayDelayMs = Mathf.Clamp(Conf.DmKeyDisplayDelayMs, 0f, 9999f);
        dmMinLitSeconds = Mathf.Clamp(Conf.DmMinLitMs, 0f, 500f) / 1000f;
    }
    private static void ExtendDmBounds(DmNoteSpec spec, ref float minX, ref float minY, ref float maxX, ref float maxY) {
        minX = Mathf.Min(minX, spec.X);
        minY = Mathf.Min(minY, spec.Y);
        maxX = Mathf.Max(maxX, spec.X + spec.W);
        maxY = Mathf.Max(maxY, spec.Y + spec.H);
        if(spec.IsStat) return;
        float noteW = spec.NoteW > 0.5f ? spec.NoteW : spec.W;
        float align = DmNoteAlignOffset(spec.W, noteW, spec.NoteAlignment);
        minX = Mathf.Min(minX, spec.X + align + spec.NoteOffsetX);
        maxX = Mathf.Max(maxX, spec.X + align + spec.NoteOffsetX + noteW);
        if(spec.NoteOffsetY < 0f) {
            minY = Mathf.Min(minY, spec.Y + spec.NoteOffsetY);
        } else if(spec.NoteOffsetY > 0f) {
            maxY = Mathf.Max(maxY, spec.Y + spec.H + spec.NoteOffsetY);
        }
    }
    private static void ResolveDmTrackGeometry(DmNoteSpec spec, float topMostY) {
        if(spec.IsStat) return;
        spec.NoteW = spec.NoteW > 0.5f ? spec.NoteW : spec.W;
        float align = DmNoteAlignOffset(spec.W, spec.NoteW, spec.NoteAlignment);
        spec.TrackX = spec.X + align + spec.NoteOffsetX;
        spec.TrackBottomY = (spec.NoteAutoYCorrection ? topMostY : spec.Y) + spec.NoteOffsetY;
    }
    private static float DmNoteAlignOffset(float keyWidth, float noteWidth, string align) {
        if(string.Equals(align, "left", StringComparison.OrdinalIgnoreCase)) return 0f;
        if(string.Equals(align, "right", StringComparison.OrdinalIgnoreCase)) return keyWidth - noteWidth;
        return (keyWidth - noteWidth) * 0.5f;
    }
    private static void ResolveDmNoteColors(JObject p, bool glow, out Color top, out Color bottom) {
        string opacityKey = glow ? "noteGlowOpacity" : "noteOpacity";
        string opacityTopKey = glow ? "noteGlowOpacityTop" : "noteOpacityTop";
        string opacityBottomKey = glow ? "noteGlowOpacityBottom" : "noteOpacityBottom";
        float baseOpacity = JFloat(p, opacityKey, glow ? 70f : 80f);
        float opacityTop = Mathf.Clamp01(JFloat(p, opacityTopKey, baseOpacity) / 100f);
        float opacityBottom = Mathf.Clamp01(JFloat(p, opacityBottomKey, baseOpacity) / 100f);
        JToken color = p?[glow ? "noteGlowColor" : "noteColor"];
        if(glow && color == null) color = p?["noteColor"];
        if(color is JObject obj && string.Equals(JStr(obj, "type", ""), "gradient", StringComparison.OrdinalIgnoreCase)) {
            top = HexToColor(JStr(obj, "top", "#FFFFFF"), opacityTop);
            bottom = HexToColor(JStr(obj, "bottom", "#FFFFFF"), opacityBottom);
            return;
        }
        string solid = color == null || color.Type == JTokenType.Null ? "#FFFFFF" : color.ToString();
        top = HexToColor(solid, opacityTop);
        bottom = HexToColor(solid, opacityBottom);
    }
    private static DmNoteSpec ParseGraphSpec(JObject p) {
        DmNoteSpec spec = new() {
            IsGraph = true,
            X = JFloat(p, "dx", 0f),
            Y = JFloat(p, "dy", 0f),
            W = Mathf.Max(1f, JFloat(p, "width", 200f)),
            H = Mathf.Max(1f, JFloat(p, "height", 100f)),
            GraphType = JStr(p, "graphType", "line"),
            GraphStat = JStr(p, "statType", "kps"),
            GraphSpeed = Mathf.Clamp(JFloat(p, "graphSpeed", 1000f), 500f, 5000f),
            GraphColor = HexToColor(JStr(p, "graphColor", "#86EFAC"), 1f),
            GraphShowAvg = JBool(p, "showAvgLine", true),
            GraphAnim = JBool(p, "graphAnimationEnabled", true),
            GraphBg = HexToColor(JStr(p, "backgroundColor", "rgba(17, 17, 20, 0.9)"), 0.9f),
            GraphBorder = HexToColor(JStr(p, "borderColor", "rgba(255, 255, 255, 0.1)"), 0.1f),
            GraphBorderWidth = Mathf.Clamp(JFloat(p, "borderWidth", 3f), 0f, 20f),
            GraphBorderRadius = Mathf.Clamp(JFloat(p, "borderRadius", 8f), 0f, 100f),
            GraphInlineStyles = JBool(p, "useInlineStyles", false),
            ClassName = JOptionalString(p, "className") ?? "",
            InactiveImage = JOptionalString(p, "inactiveImage") ?? "",
            ActiveImage = JOptionalString(p, "activeImage") ?? "",
            IdleImageFit = JStr(p, "idleImageFit", ""),
            ImageFitDefault = JStr(p, "imageFit", ""),
        };
        spec.CountKey = "graph";
        spec.DisplayText = "";
        return spec;
    }
    private static DmNoteSpec ParseDmNoteSpec(string keyName, JObject p, bool stat) {
        string fontHex = JStr(p, "fontColor", "rgba(121, 121, 121, 0.9)");
        string activeFontHex = JStr(p, "activeFontColor", "#FFFFFF");
        string bgHex = JStr(p, "backgroundColor", "rgba(46, 46, 47, 0.9)");
        string activeBgHex = JStr(p, "activeBackgroundColor", "rgba(121, 121, 121, 0.9)");
        string borderHex = JStr(p, "borderColor", "rgba(113, 113, 113, 0.9)");
        string activeBorderHex = JStr(p, "activeBorderColor", "rgba(255, 255, 255, 0.9)");
        JObject counter = p["counter"] as JObject;
        JObject counterFill = counter?["fill"] as JObject;
        JObject counterStroke = counter?["stroke"] as JObject;
        DmNoteSpec spec = new() {
            KeyName = keyName ?? "",
            X = JFloat(p, "dx", 0f),
            Y = JFloat(p, "dy", 0f),
            W = Mathf.Max(1f, JFloat(p, "width", stat ? 100f : 60f)),
            H = Mathf.Max(1f, JFloat(p, "height", stat ? 30f : 60f)),
            IsStat = stat,
        };
        spec.KeyCode = stat ? KeyCode.None : ResolveDmNoteKeyCode(spec.KeyName);
        string ghost = JOptionalString(p, "ghostKey");
        spec.GhostKeyCode = string.IsNullOrEmpty(ghost) ? KeyCode.None : ResolveDmNoteKeyCode(ghost);
        spec.CountKey = JOptionalString(p, "countKey");
        if(string.IsNullOrEmpty(spec.CountKey)) spec.CountKey = spec.KeyName;
        spec.ClassName = JOptionalString(p, "className") ?? "";
        spec.InactiveImage = JOptionalString(p, "inactiveImage") ?? "";
        spec.ActiveImage = JOptionalString(p, "activeImage") ?? "";
        spec.IdleImageFit = JStr(p, "idleImageFit", "");
        spec.ActiveImageFit = JStr(p, "activeImageFit", "");
        spec.ImageFitDefault = JStr(p, "imageFit", "");
        spec.Bg = HexToColor(bgHex, 0.9f);
        spec.ActiveBg = HexToColor(activeBgHex, 0.9f);
        if(JBool(p, "idleTransparent", false)) spec.Bg.a = 0f;
        if(JBool(p, "activeTransparent", false)) spec.ActiveBg.a = 0f;
        spec.Outline = HexToColor(borderHex, 0.9f);
        spec.ActiveOutline = HexToColor(activeBorderHex, spec.Outline.a);
        spec.Text = HexToColor(fontHex, 1f);
        spec.ActiveText = HexToColor(activeFontHex, 1f);
        spec.BorderRadius = Mathf.Clamp(JFloat(p, "borderRadius", 10f), 0f, 100f);
        spec.BoxBorderWidth = Mathf.Clamp(JFloat(p, "borderWidth", 3f), 0f, 20f);
        if(spec.BoxBorderWidth <= 0.01f) {
            spec.Outline.a = 0f;
            spec.ActiveOutline.a = 0f;
        }
        ResolveDmNoteColors(p, false, out spec.RainTop, out spec.RainBottom);
        spec.Rain = spec.RainBottom;
        spec.RainGlowOn = JBool(p, "noteGlowEnabled", false);
        spec.RainGlowSize = Mathf.Clamp(JFloat(p, "noteGlowSize", 20f), 0f, 50f);
        ResolveDmNoteColors(p, true, out spec.RainGlowTop, out spec.RainGlowBottom);
        spec.RainShadowOn = JBool(p, "quartzNoteShadow", false);
        spec.RainShadowColor = HexToColor(JStr(p, "quartzNoteShadowColor", "rgba(0, 0, 0, 0.5)"), 0.5f);
        spec.RainShadowX = Mathf.Clamp(JFloat(p, "quartzNoteShadowX", 3f), -64f, 64f);
        spec.RainShadowY = Mathf.Clamp(JFloat(p, "quartzNoteShadowY", -3f), -64f, 64f);
        spec.NoteBorderWidth = Mathf.Clamp(JFloat(p, "noteBorderWidth", 0f), 0f, 20f);
        Color noteBorder = HexToColor(JStr(p, "noteBorderColor", "#FFFFFF"), 1f);
        noteBorder.a = Mathf.Clamp01(JFloat(p, "noteBorderOpacity", 100f) / 100f);
        spec.NoteBorderColor = noteBorder;
        spec.NoteBorderSide = JStr(p, "noteBorderSide", "all").ToLowerInvariant() switch {
            "vertical" => 1,
            "horizontal" => 2,
            _ => 0,
        };
        spec.NoteRadius = Mathf.Clamp(JFloat(p, "noteBorderRadius", 0f), 0f, 60f);
        spec.LabelFontStyles = DmFontStyles(
            JInt(p, "fontWeight", 400), JBool(p, "fontItalic", false),
            JBool(p, "fontUnderline", false), JBool(p, "fontStrikethrough", false));
        if(counter != null) {
            spec.CounterFontStyles = DmFontStyles(
                JInt(counter, "fontWeight", 400), JBool(counter, "fontItalic", false),
                JBool(counter, "fontUnderline", false), JBool(counter, "fontStrikethrough", false));
            if(counter["animation"] is JObject anim) {
                spec.CounterAnimEnabled = JBool(anim, "enabled", true);
                spec.CounterAnimScale = Mathf.Clamp(JFloat(anim, "scale", 1.1f), 0.25f, 4f);
                spec.CounterAnimDurationMs = Mathf.Clamp(JFloat(anim, "durationMs", 300f), 1f, 5000f);
                if(anim["bezier"] is JArray bz && bz.Count == 4) {
                    spec.CounterAnimBezier = new Vector4(
                        Mathf.Clamp01(JTokenFloat(bz[0], 0.25f)), JTokenFloat(bz[1], 0.46f),
                        Mathf.Clamp01(JTokenFloat(bz[2], 0.45f)), JTokenFloat(bz[3], 0.94f));
                }
            }
        }
        float pressScale = Mathf.Clamp(JFloat(p, "quartzPressScale", 1f), 0.25f, 2f);
        if(Mathf.Abs(pressScale - 1f) > 0.001f && spec.ActiveScale == Vector2.one)
            spec.ActiveScale = new Vector2(pressScale, pressScale);
        string ghostNoteHex = JOptionalString(p, "ghostNoteColor");
        if(string.IsNullOrEmpty(ghostNoteHex)) {
            spec.GhostRainTop = new Color(spec.RainTop.r, spec.RainTop.g, spec.RainTop.b, spec.RainTop.a * 0.45f);
            spec.GhostRainBottom = new Color(spec.RainBottom.r, spec.RainBottom.g, spec.RainBottom.b, spec.RainBottom.a * 0.45f);
            spec.GhostRainGlowTop = new Color(spec.RainGlowTop.r, spec.RainGlowTop.g, spec.RainGlowTop.b, spec.RainGlowTop.a * 0.45f);
            spec.GhostRainGlowBottom = new Color(spec.RainGlowBottom.r, spec.RainGlowBottom.g, spec.RainGlowBottom.b, spec.RainGlowBottom.a * 0.45f);
        } else {
            Color ghostColor = HexToColor(ghostNoteHex, JFloat(p, "ghostNoteOpacity", 45f) / 100f);
            spec.GhostRainTop = ghostColor;
            spec.GhostRainBottom = ghostColor;
            spec.GhostRainGlowTop = ghostColor;
            spec.GhostRainGlowBottom = ghostColor;
        }
        spec.GhostRain = spec.GhostRainBottom;
        spec.FontSize = JInt(p, "fontSize", stat ? 16 : 18);
        spec.CounterEnabled = Conf.DmShowCounter && (counter != null ? JBool(counter, "enabled", true) : true);
        spec.CounterFontSize = counter != null
            ? JInt(counter, "fontSize", 16)
            : 16;
        spec.CounterAlign = counter != null ? JStr(counter, "align", "top") : "top";
        spec.CounterAlignMode = counter != null ? JStr(counter, "alignMode", "center") : "center";
        spec.CounterGap = counter != null ? JFloat(counter, "gap", 6f) : 6f;
        spec.CounterOutside = counter != null && string.Equals(JStr(counter, "placement", "inside"), "outside", StringComparison.OrdinalIgnoreCase);
        string counterIdle = counterFill != null ? JStr(counterFill, "idle", fontHex) : fontHex;
        string counterActive = counterFill != null ? JStr(counterFill, "active", activeFontHex) : activeFontHex;
        spec.CounterText = HexToColor(counterIdle, 1f);
        spec.ActiveCounterText = HexToColor(counterActive, 1f);
        spec.CounterStroke = HexToColor(counterStroke != null ? JStr(counterStroke, "idle", "transparent") : "transparent", 0f);
        spec.ActiveCounterStroke = HexToColor(counterStroke != null ? JStr(counterStroke, "active", "transparent") : "transparent", 0f);
        spec.NoteEnabled = JBool(p, "noteEffectEnabled", true);
        spec.NoteW = JFloat(p, "noteWidth", spec.W);
        spec.NoteAlignment = JStr(p, "noteAlignment", "center");
        spec.NoteOffsetX = Mathf.Clamp(JFloat(p, "noteOffsetX", 0f), -500f, 500f);
        spec.NoteOffsetY = Mathf.Clamp(JFloat(p, "noteOffsetY", 0f), -500f, 500f);
        spec.NoteAutoYCorrection = JBool(p, "noteAutoYCorrection", true);
        spec.IsKps = stat && spec.KeyName.Equals("kps", StringComparison.OrdinalIgnoreCase);
        spec.IsKpsAvg = stat && spec.KeyName.Equals("kpsAvg", StringComparison.OrdinalIgnoreCase);
        spec.IsKpsMax = stat && spec.KeyName.Equals("kpsMax", StringComparison.OrdinalIgnoreCase);
        spec.IsTotal = stat && spec.KeyName.Equals("total", StringComparison.OrdinalIgnoreCase);
        spec.InlineStatCounter = stat && spec.CounterEnabled
            && !spec.CounterOutside
            && string.Equals(spec.CounterAlignMode, "center", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(spec.CounterAlign, "top", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(spec.CounterAlign, "bottom", StringComparison.OrdinalIgnoreCase);
        string display = JOptionalString(p, "displayText");
        spec.DisplayText = !string.IsNullOrEmpty(display)
            ? display
            : DefaultDmNoteDisplay(spec.KeyName, stat);
        return spec;
    }
    private static string DefaultDmNoteDisplay(string keyName, bool stat) {
        if(stat) {
            if(keyName.Equals("kps", StringComparison.OrdinalIgnoreCase)) return "KPS";
            if(keyName.Equals("kpsAvg", StringComparison.OrdinalIgnoreCase)) return "AVG";
            if(keyName.Equals("kpsMax", StringComparison.OrdinalIgnoreCase)) return "MAX";
            if(keyName.Equals("total", StringComparison.OrdinalIgnoreCase)) return "Total";
            return keyName.ToUpperInvariant();
        }
        if(string.IsNullOrEmpty(keyName)) return "";
        KeyCode key = ResolveDmNoteKeyCode(keyName);
        return key == KeyCode.None ? keyName : KeyCodeShortLabel(key);
    }
    internal static KeyCode ResolveGlobalKey(string name) => ResolveDmNoteKeyCode(name);
    private static KeyCode ResolveDmNoteKeyCode(string name) {
        if(string.IsNullOrEmpty(name)) return KeyCode.None;
        if(name.Length > 1 && int.TryParse(name, out int numeric)) return Features.KeyLimiter.KeyLimiter.NormalizeNumericKey(numeric);
        string normalized = name.Replace(" ", "").Replace("_", "").Replace("-", "");
        if(normalized.StartsWith("KEY", StringComparison.OrdinalIgnoreCase) && normalized.Length == 4) normalized = normalized[3..];
        if(normalized.StartsWith("DIGIT", StringComparison.OrdinalIgnoreCase) && normalized.Length == 6) normalized = normalized[5..];
        if(normalized.StartsWith("NUMPAD", StringComparison.OrdinalIgnoreCase) && normalized.Length > 6) {
            string np = normalized.Substring(6).ToUpperInvariant();
            KeyCode npk = np switch {
                "ENTER" or "RETURN" => KeyCode.KeypadEnter,
                "PLUS" or "ADD" => KeyCode.KeypadPlus,
                "MINUS" or "SUBTRACT" => KeyCode.KeypadMinus,
                "MULTIPLY" or "STAR" or "ASTERISK" => KeyCode.KeypadMultiply,
                "DIVIDE" or "SLASH" => KeyCode.KeypadDivide,
                "DELETE" or "DECIMAL" or "PERIOD" or "DOT" or "DEL" => KeyCode.KeypadPeriod,
                "EQUALS" or "EQUAL" => KeyCode.KeypadEquals,
                _ => np.Length == 1 && np[0] >= '0' && np[0] <= '9'
                    ? (KeyCode)((int)KeyCode.Keypad0 + (np[0] - '0'))
                    : KeyCode.None,
            };
            if(npk != KeyCode.None) return npk;
        }
        if(!char.IsDigit(normalized[0]) && Enum.TryParse(normalized, true, out KeyCode parsed)) return Features.KeyLimiter.KeyLimiter.NormalizeKey(parsed);
        switch(normalized.ToUpperInvariant()) {
            case "DOT":
            case "PERIOD": return KeyCode.Period;
            case "ENTER": return KeyCode.Return;
            case "ESC": return KeyCode.Escape;
            case "LEFTSHIFT": return KeyCode.LeftShift;
            case "RIGHTSHIFT": return KeyCode.RightShift;
            case "LCONTROL":
            case "LEFTCONTROL":
            case "LEFTCTRL":
            case "CTRL":
            case "CONTROL":
            case "LCTRL": return KeyCode.LeftControl;
            case "RCONTROL":
            case "RIGHTCONTROL":
            case "RIGHTCTRL":
            case "RCTRL":
            case "HANJA": return KeyCode.RightControl;
            case "RALT":
            case "RIGHTALT":
            case "ALTGR":
            case "HANGUL": return KeyCode.RightAlt;
            case "LALT":
            case "LEFTALT": return KeyCode.LeftAlt;
            case "INS": return KeyCode.Insert;
            case "PRINTSCREEN":
            case "PRTSC":
            case "PRTSCR":
            case "SYSREQ": return KeyCode.Print;
            case "CONTEXTMENU": return KeyCode.Menu;
            case "BACKSLASH": return KeyCode.Backslash;
            case "SLASH": return KeyCode.Slash;
            case "FORWARDSLASH": return KeyCode.Slash;
            case "CAPSLOCK": return KeyCode.CapsLock;
            case "SPACE": return KeyCode.Space;
            case "SECTION": return KeyCode.BackQuote;
            case "COMMA": return KeyCode.Comma;
            case "PLUS": return KeyCode.Plus;
            case "MINUS": return KeyCode.Minus;
            case "EQUAL":
            case "EQUALS": return KeyCode.Equals;
            case "SEMICOLON": return KeyCode.Semicolon;
            case "QUOTE": return KeyCode.Quote;
            case "BACKQUOTE":
            case "BACKTICK": return KeyCode.BackQuote;
            case "SQUAREBRACKETOPEN":
            case "OPENBRACKET":
            case "LEFTBRACKET":
            case "LBRACKET": return KeyCode.LeftBracket;
            case "SQUAREBRACKETCLOSE":
            case "CLOSEBRACKET":
            case "RIGHTBRACKET":
            case "RBRACKET": return KeyCode.RightBracket;
            case "UP":
            case "UPARROW": return KeyCode.UpArrow;
            case "DOWN":
            case "DOWNARROW": return KeyCode.DownArrow;
            case "LEFT":
            case "LEFTARROW": return KeyCode.LeftArrow;
            case "RIGHT":
            case "RIGHTARROW": return KeyCode.RightArrow;
        }
        if(normalized.Length == 1) {
            char c = char.ToUpperInvariant(normalized[0]);
            if(c >= 'A' && c <= 'Z') return (KeyCode)((int)KeyCode.A + (c - 'A'));
            if(c >= '0' && c <= '9') return (KeyCode)((int)KeyCode.Alpha0 + (c - '0'));
        }
        return KeyCode.None;
    }
    internal static Color HexToColor(string hex, float alpha) {
        if(string.IsNullOrEmpty(hex)) return new Color(1f, 1f, 1f, alpha);
        string s = hex.Trim();
        try {
            if(string.Equals(s, "transparent", StringComparison.OrdinalIgnoreCase)) return new Color(0f, 0f, 0f, 0f);
            if(s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) {
                int lp = s.IndexOf('(');
                int rp = s.IndexOf(')');
                if(lp > 0 && rp > lp) {
                    string inner = s[(lp + 1)..rp];
                    string[] parts = inner.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if(parts.Length >= 3) {
                        float r = KeyViewerStylesheet.Comp(parts[0], 255f);
                        float g = KeyViewerStylesheet.Comp(parts[1], 255f);
                        float b = KeyViewerStylesheet.Comp(parts[2], 255f);
                        float a = parts.Length >= 4 ? KeyViewerStylesheet.Alpha(parts[3]) : alpha;
                        return new Color(r, g, b, a);
                    }
                }
            }
            string h = s.TrimStart('#');
            if(h.Length == 3 || h.Length == 4) {
                int r = Convert.ToInt32(new string(h[0], 2), 16);
                int g = Convert.ToInt32(new string(h[1], 2), 16);
                int b = Convert.ToInt32(new string(h[2], 2), 16);
                int a = h.Length == 4 ? Convert.ToInt32(new string(h[3], 2), 16) : Mathf.RoundToInt(alpha * 255f);
                return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            }
            if(h.Length == 6 || h.Length == 8) {
                int r = Convert.ToInt32(h[..2], 16);
                int g = Convert.ToInt32(h[2..4], 16);
                int b = Convert.ToInt32(h[4..6], 16);
                int a = h.Length == 8 ? Convert.ToInt32(h[6..8], 16) : Mathf.RoundToInt(alpha * 255f);
                return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            }
        } catch { }
        return new Color(1f, 1f, 1f, alpha);
    }
    private static string JStr(JObject p, string key, string def) {
        JToken t = p?[key];
        return t == null || t.Type == JTokenType.Null ? def : t.ToString();
    }
    private static string JOptionalString(JObject p, string key) {
        JToken t = p?[key];
        return t == null || t.Type == JTokenType.Null ? null : t.ToString();
    }
    private static T JVal<T>(JObject p, string key, T def) {
        JToken t = p?[key];
        if(t == null || t.Type == JTokenType.Null) return def;
        try { return t.ToObject<T>(); } catch { return def; }
    }
    private static float JFloat(JObject p, string key, float def) => JVal(p, key, def);
    private static int JInt(JObject p, string key, int def) => JVal(p, key, def);
    private static bool JBool(JObject p, string key, bool def) => JVal(p, key, def);
    private static float JTokenFloat(JToken t, float def) {
        if(t == null || t.Type == JTokenType.Null) return def;
        try { return t.ToObject<float>(); } catch { return def; }
    }
    private static TMPro.FontStyles DmFontStyles(int weight, bool italic, bool underline, bool strikethrough) {
        TMPro.FontStyles styles = TMPro.FontStyles.Normal;
        if(weight >= 600) styles |= TMPro.FontStyles.Bold;
        if(italic) styles |= TMPro.FontStyles.Italic;
        if(underline) styles |= TMPro.FontStyles.Underline;
        if(strikethrough) styles |= TMPro.FontStyles.Strikethrough;
        return styles;
    }
}
