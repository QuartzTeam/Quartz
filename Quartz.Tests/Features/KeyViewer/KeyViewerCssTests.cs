using Quartz.Features.KeyViewer;
using static Asserts;
static class KeyViewerCssTests {
    public static void TestKeyViewerCss() {
        const string neon = """
            /* comment */
            [data-state="inactive"], [data-state="active"] {
              --key-radius: 6px;
              font-size: 24px;
              font-weight: 700;
            }
            [data-state="inactive"] {
              --key-bg: transparent;
              --key-border: 3px solid #474244;
              --key-text-color: #474244;
            }
            [data-state="active"] {
              --key-bg: transparent;
              --key-border: 3px solid #ff2b80;
              --key-text-color: #ff2b80;
              text-shadow: 0px 0px 3px #ff2b80;
              box-shadow: 0px 0px 4px #b3245d, 0px 0px 4px #b3245d;
            }
            .blue[data-state="active"] {
              --key-border: 3px solid #2ebef3 !important;
              --key-text-color: #2ebef3;
            }
            .counter[data-counter-state="active"] { --counter-color: #ff2b80; }
            .blue .counter[data-counter-state="active"] { --counter-color: #2ebef3; }
            """;
        KeyViewerStylesheet sheet = KeyViewerStylesheet.Parse(neon);
        Assert(!sheet.IsEmpty, "stylesheet should not be empty");
        CssKeyStyleSet plain = sheet.ResolveKey(null);
        Assert(plain.Idle.Radius == 6f, "global radius applies to idle");
        Assert(plain.Active.Radius == 6f, "global radius applies to active");
        Assert(plain.Idle.FontSize == 24f, "font-size parsed");
        Assert(plain.Active.Bold == true, "font-weight 700 is bold");
        Assert(plain.Idle.Bg.Has && plain.Idle.Bg.A == 0f, "transparent bg has alpha 0");
        Assert(plain.Active.BorderWidth == 3f, "border width from shorthand");
        Assert(ColorEq(plain.Active.BorderColor, 0xff, 0x2b, 0x80), "active border colour");
        Assert(plain.Idle.TextShadow.On == false, "idle has no text-shadow");
        Assert(plain.Active.TextShadow.On && plain.Active.TextShadow.Blur == 3f, "active text-shadow blur");
        Assert(plain.Active.BoxShadow.On && plain.Active.BoxShadow.Blur == 4f, "active box-shadow blur");
        CssKeyStyleSet blue = sheet.ResolveKey("blue");
        Assert(ColorEq(blue.Active.BorderColor, 0x2e, 0xbe, 0xf3), "blue border overrides global (!important stripped)");
        Assert(blue.Active.Radius == 6f, "blue still inherits global radius");
        CssCounterStyleSet ctr = sheet.ResolveCounter(null);
        Assert(ColorEq(ctr.Active.Color, 0xff, 0x2b, 0x80), "global counter colour");
        CssCounterStyleSet blueCtr = sheet.ResolveCounter("blue");
        Assert(ColorEq(blueCtr.Active.Color, 0x2e, 0xbe, 0xf3), "blue counter colour override");
        KeyViewerStylesheet strokeSheet = KeyViewerStylesheet.Parse(
            ".counter[data-counter-state=\"active\"] { --counter-stroke-color: #333; --counter-stroke-width: 2px; }");
        CssCounterStyle strokeActive = strokeSheet.ResolveCounter(null).Active;
        Assert(strokeActive.StrokeWidth == 2f, "counter stroke width parsed");
        Assert(ColorEq(strokeActive.StrokeColor, 0x33, 0x33, 0x33), "counter stroke colour parsed");
        KeyViewerStylesheet rgba = KeyViewerStylesheet.Parse(
            "[data-state=\"inactive\"] { --key-text-color: rgba(121, 121, 121, 0.5); }");
        CssColor txt = rgba.ResolveKey(null).Idle.TextColor;
        Assert(txt.Has && Math.Abs(txt.A - 0.5f) < 0.01f, "rgba alpha parsed");
        const string rainbow = """
            @font-face { font-family: 'X'; src: url('https://example/x.woff2'); }
            [data-state="active"] > * {
              background: linear-gradient(45deg, #ff0000, #ff7300, #fffb00, #48ff00);
              -webkit-background-clip: text;
              -webkit-text-fill-color: transparent;
              animation: spin 5s linear infinite;
            }
            @keyframes spin { 0% { background-position: 0 0; } 100% { background-position: 400% 0; } }
            """;
        KeyViewerStylesheet rb = KeyViewerStylesheet.Parse(rainbow);
        CssKeyStyle act = rb.ResolveKey(null).Active;
        Assert(act.TextGradient != null, "gradient mapped to text via clip:text");
        Assert(act.TextGradient!.Stops.Count == 4, "four gradient stops parsed");
        Assert(act.TextGradient!.Animated && Math.Abs(act.TextGradient!.AnimSeconds - 5f) < 0.01f, "animation duration parsed");
        Assert(act.BgGradient == null, "clip:text keeps gradient off the box fill");
        Assert(KeyViewerStylesheet.Parse("@keyframes a { 0% { opacity: 0; } }").IsEmpty, "keyframes-only sheet is empty");
    }
    public static void TestKeyViewerCssExtended() {
        const string css = """
            @font-face { font-family: 'OmuDaye'; src: url('https://cdn/omyu.woff2') format('woff2'); }
            [data-state="active"] {
              transform: scale(1.05) translate(0px, 2px);
              filter: brightness(1.2) saturate(1.5);
              transition: all 0.1s ease-in-out;
              mix-blend-mode: screen;
              backdrop-filter: blur(10px);
            }
            [data-state="inactive"]:before {
              content: "";
              background: linear-gradient(45deg, #ff0000, #00ff00, #0000ff);
              inset: -2px;
              filter: blur(3px);
              z-index: -1;
              animation: glow 20s linear infinite;
            }
            [data-state="active"]:after {
              background: linear-gradient(135deg, #111111, #252525);
              z-index: 0;
            }
            .blue.special[data-state="active"] { --key-text-color: #abcdef; }
            """;
        KeyViewerStylesheet sheet = KeyViewerStylesheet.Parse(css);
        Assert(sheet.FontFaces.Count == 1, "one @font-face captured");
        Assert(sheet.FontFaces[0].Family == "OmuDaye", "font-face family");
        Assert(sheet.FontFaces[0].Srcs.Count == 1 && sheet.FontFaces[0].Srcs[0].EndsWith("omyu.woff2"), "font-face src url");
        CssKeyStyle a = sheet.ResolveKey(null).Active;
        Assert(a.Transform != null && Math.Abs(a.Transform!.ScaleX - 1.05f) < 0.001f, "transform scale");
        Assert(Math.Abs(a.Transform!.TranslateY - 2f) < 0.001f, "transform translateY");
        Assert(a.Filter != null && Math.Abs(a.Filter!.Brightness - 1.2f) < 0.001f, "filter brightness");
        Assert(Math.Abs(a.Filter!.Saturate - 1.5f) < 0.001f, "filter saturate");
        Assert(a.TransitionSeconds.HasValue && Math.Abs(a.TransitionSeconds!.Value - 0.1f) < 0.001f, "transition duration");
        Assert(a.Blend == CssBlend.Screen, "mix-blend-mode screen");
        Assert(a.BackdropBlur.HasValue && Math.Abs(a.BackdropBlur!.Value - 10f) < 0.001f, "backdrop blur");
        CssLayer? before = sheet.ResolveKey(null).Idle.Before;
        Assert(before != null, ":before layer captured");
        Assert(before!.Gradient != null && before.Gradient!.Stops.Count == 3, ":before gradient stops");
        Assert(before.Gradient!.Animated && Math.Abs(before.Gradient!.AnimSeconds - 20f) < 0.001f, ":before animation");
        Assert(Math.Abs(before.InsetT + 2f) < 0.001f, ":before inset -2px");
        Assert(Math.Abs(before.Blur - 3f) < 0.001f, ":before filter blur");
        Assert(before.Z == -1, ":before z-index");
        CssLayer? after = sheet.ResolveKey(null).Active.After;
        Assert(after != null && after!.Gradient != null, ":after gradient");
        Assert(after!.Z == 0, ":after z-index 0");
        Assert(!sheet.ResolveKey("blue").Active.TextColor.Has, "compound rule needs all classes");
        Assert(sheet.ResolveKey("blue special").Active.TextColor.Has, "compound rule matches when all present");
        Assert(KeyViewerStylesheet.TryParseColor("hsl(120, 100%, 50%)", out CssColor green)
            && green.G > 0.9f && green.R < 0.1f, "hsl parsed to green");
        KeyViewerStylesheet gsheet = KeyViewerStylesheet.Parse("""
            .kps-graph {
              --graph-bg: rgba(7, 10, 18, 0.72);
              --graph-border: 1px solid rgba(125, 211, 252, 0.55);
              --graph-radius: 12px;
              --graph-color: #7dd3fc;
            }
            """);
        CssGraphStyle g = gsheet.ResolveGraph("kps-graph");
        Assert(g.Bg.Has && Math.Abs(g.Bg.A - 0.72f) < 0.02f, "--graph-bg parsed");
        Assert(g.BorderWidth == 1f && g.BorderColor.Has, "--graph-border shorthand");
        Assert(g.Radius == 12f, "--graph-radius parsed");
        Assert(ColorEq(g.Color, 0x7d, 0xd3, 0xfc), "--graph-color parsed");
        Assert(!gsheet.ResolveGraph("other").Any, "graph class is scoped");
    }
    public static void TestKeyViewerPersistence() {
        float firstDeadline = KeyViewerPersistence.CountSaveDeadline(10f);
        float secondDeadline = KeyViewerPersistence.CountSaveDeadline(11f);
        Assert(firstDeadline == 12f && secondDeadline == 13f, "new presses extend count-save debounce");
        Assert(!KeyViewerPersistence.ShouldFlushCounts(true, true, 20f, secondDeadline), "counts never flush during gameplay");
        Assert(!KeyViewerPersistence.ShouldFlushCounts(true, false, 12.9f, secondDeadline), "counts wait for idle deadline");
        Assert(KeyViewerPersistence.ShouldFlushCounts(true, false, 13f, secondDeadline), "idle counts flush outside gameplay");
        Assert(KeyViewerPersistence.ShouldPersistBoxCount(false, false), "main key counts persist");
        Assert(!KeyViewerPersistence.ShouldPersistBoxCount(false, true), "foot bindings cannot overwrite main key counts");
        const string preset = """
            {
              "selectedKeyType": "4key",
              "keys": { "4key": ["A"] },
              "keyPositions": { "4key": [{ "dx": 1, "inactiveImage": "data:image/png;base64,kept" }] },
              "statPositions": { "4key": [] },
              "graphPositions": { "4key": [] },
              "embeddedLocalFonts": { "huge": "discard" },
              "embeddedLocalSounds": { "huge": "discard" },
              "customJS": "discard"
            }
            """;
        string sanitized = KeyViewerPersistence.SanitizeDmPreset(preset);
        Assert(sanitized.Contains("selectedKeyType") && sanitized.Contains("inactiveImage"), "runtime preset fields survive sanitization");
        Assert(!sanitized.Contains("embeddedLocalFonts") && !sanitized.Contains("embeddedLocalSounds") && !sanitized.Contains("customJS"),
            "unused embedded preset payloads are discarded");
        bool invalidRejected = false;
        try { KeyViewerPersistence.SanitizeDmPreset("{\"embeddedLocalFonts\":{}}"); }
        catch(FormatException) { invalidRejected = true; }
        Assert(invalidRejected, "preset import validates required runtime tables");
    }
    static bool ColorEq(CssColor c, int r, int g, int b) =>
        c.Has
        && Math.Abs(c.R - r / 255f) < 0.02f
        && Math.Abs(c.G - g / 255f) < 0.02f
        && Math.Abs(c.B - b / 255f) < 0.02f;
}
