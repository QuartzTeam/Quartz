using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerSettings : ISettingsFile {
    public JToken Serialize() {
        JObject counts = [];
        foreach((string key, int value) in Counts) counts[key] = value;
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(ShowOutsideGame)] = ShowOutsideGame,
            [nameof(Mode)] = NormalizeMode(Mode),
            [nameof(Style)] = Style,
            [nameof(Size)] = Size,
            [nameof(OffsetX)] = OffsetX,
            [nameof(OffsetY)] = OffsetY,
            [nameof(SyncToKeyLimiter)] = SyncToKeyLimiter,
            [nameof(RainEnabled)] = RainEnabled,
            [nameof(RainSpeed)] = RainSpeed,
            [nameof(RainHeight)] = RainHeight,
            [nameof(RainFade)] = RainFade,
            [nameof(RainWidth)] = RainWidth,
            [nameof(Rain2Width)] = Rain2Width,
            [nameof(RainOffsetY)] = RainOffsetY,
            [nameof(Rain2OffsetY)] = Rain2OffsetY,
            [nameof(StatsTogether)] = StatsTogether,
            [nameof(CountFormatting)] = CountFormatting,
            [nameof(HideMainKeyCount)] = HideMainKeyCount,
            [nameof(PerKeyKps)] = PerKeyKps,
            [nameof(StreamerMode)] = StreamerMode,
            [nameof(RainR)] = RainR, [nameof(RainG)] = RainG, [nameof(RainB)] = RainB, [nameof(RainA)] = RainA,
            [nameof(Rain2R)] = Rain2R, [nameof(Rain2G)] = Rain2G, [nameof(Rain2B)] = Rain2B, [nameof(Rain2A)] = Rain2A,
            [nameof(Rain3R)] = Rain3R, [nameof(Rain3G)] = Rain3G, [nameof(Rain3B)] = Rain3B, [nameof(Rain3A)] = Rain3A,
            [nameof(DmPresetJson)] = DmPresetJson,
            [nameof(DmSelectedTab)] = DmSelectedTab,
            [nameof(DmOffsetX)] = DmOffsetX,
            [nameof(DmOffsetY)] = DmOffsetY,
            [nameof(DmScale)] = DmScale,
            [nameof(DmNoteEffect)] = DmNoteEffect,
            [nameof(DmNoteSpeed)] = DmNoteSpeed,
            [nameof(DmTrackHeight)] = DmTrackHeight,
            [nameof(DmNoteReverse)] = DmNoteReverse,
            [nameof(DmShowCounter)] = DmShowCounter,
            [nameof(DmFadePx)] = DmFadePx,
            [nameof(DmFadeTopPx)] = DmFadeTopPx,
            [nameof(DmFadeBottomPx)] = DmFadeBottomPx,
            [nameof(DmReverseFadeTopPx)] = DmReverseFadeTopPx,
            [nameof(DmReverseFadeBottomPx)] = DmReverseFadeBottomPx,
            [nameof(DmDelayedNoteEnabled)] = DmDelayedNoteEnabled,
            [nameof(DmShortNoteThresholdMs)] = DmShortNoteThresholdMs,
            [nameof(DmShortNoteMinLengthPx)] = DmShortNoteMinLengthPx,
            [nameof(DmKeyDisplayDelayMs)] = DmKeyDisplayDelayMs,
            [nameof(DmMinLitMs)] = DmMinLitMs,
            [nameof(DmCssEnabled)] = DmCssEnabled,
            [nameof(DmCssText)] = DmCssText,
            [nameof(DmCssPath)] = DmCssPath,
            [nameof(Key10)] = new JArray(Key10),
            [nameof(Key12)] = new JArray(Key12),
            [nameof(Key16)] = new JArray(Key16),
            [nameof(Key20)] = new JArray(Key20),
            [nameof(Key8)] = new JArray(Key8),
            [nameof(Key14)] = new JArray(Key14),
            [nameof(Key10Text)] = WriteLabels(Key10Text),
            [nameof(Key12Text)] = WriteLabels(Key12Text),
            [nameof(Key16Text)] = WriteLabels(Key16Text),
            [nameof(Key20Text)] = WriteLabels(Key20Text),
            [nameof(Key8Text)] = WriteLabels(Key8Text),
            [nameof(Key14Text)] = WriteLabels(Key14Text),
            [nameof(BgR)] = BgR, [nameof(BgG)] = BgG, [nameof(BgB)] = BgB, [nameof(BgA)] = BgA,
            [nameof(BgPressedR)] = BgPressedR, [nameof(BgPressedG)] = BgPressedG, [nameof(BgPressedB)] = BgPressedB, [nameof(BgPressedA)] = BgPressedA,
            [nameof(OutlineR)] = OutlineR, [nameof(OutlineG)] = OutlineG, [nameof(OutlineB)] = OutlineB, [nameof(OutlineA)] = OutlineA,
            [nameof(OutlinePressedR)] = OutlinePressedR, [nameof(OutlinePressedG)] = OutlinePressedG, [nameof(OutlinePressedB)] = OutlinePressedB, [nameof(OutlinePressedA)] = OutlinePressedA,
            [nameof(TextR)] = TextR, [nameof(TextG)] = TextG, [nameof(TextB)] = TextB, [nameof(TextA)] = TextA,
            [nameof(TextPressedR)] = TextPressedR, [nameof(TextPressedG)] = TextPressedG, [nameof(TextPressedB)] = TextPressedB, [nameof(TextPressedA)] = TextPressedA,
            [nameof(KeyFontScale)] = KeyFontScale,
            [nameof(CounterFontScale)] = CounterFontScale,
            [nameof(PerKeyFontEnabled)] = new JArray(PerKeyFontEnabled),
            [nameof(PerKeyFontInit)] = new JArray(PerKeyFontInit),
            [nameof(PerKeyKeyFont)] = new JArray(PerKeyKeyFont),
            [nameof(PerKeyCounterFont)] = new JArray(PerKeyCounterFont),
            [nameof(PerKeyColorEnabled)] = new JArray(PerKeyColorEnabled),
            [nameof(PerKeyColorInit)] = new JArray(PerKeyColorInit),
            [nameof(PerKeyBg)] = WriteColors(PerKeyBg),
            [nameof(PerKeyBgPressed)] = WriteColors(PerKeyBgPressed),
            [nameof(PerKeyOutline)] = WriteColors(PerKeyOutline),
            [nameof(PerKeyOutlinePressed)] = WriteColors(PerKeyOutlinePressed),
            [nameof(PerKeyText)] = WriteColors(PerKeyText),
            [nameof(PerKeyTextPressed)] = WriteColors(PerKeyTextPressed),
            [nameof(PerKeyRain)] = WriteColors(PerKeyRain),
            [nameof(GhostRainR)] = GhostRainR, [nameof(GhostRainG)] = GhostRainG, [nameof(GhostRainB)] = GhostRainB, [nameof(GhostRainA)] = GhostRainA,
            [nameof(GhostRainDotted)] = GhostRainDotted,
            [nameof(GhostRainDotLength)] = GhostRainDotLength,
            [nameof(GhostRainGapLength)] = GhostRainGapLength,
            [nameof(GhostKey8)] = new JArray(GhostKey8),
            [nameof(GhostKey10)] = new JArray(GhostKey10),
            [nameof(GhostKey12)] = new JArray(GhostKey12),
            [nameof(GhostKey14)] = new JArray(GhostKey14),
            [nameof(GhostKey16)] = new JArray(GhostKey16),
            [nameof(GhostKey20)] = new JArray(GhostKey20),
            [nameof(FootStyle)] = FootStyle,
            [nameof(FootOffsetX)] = FootOffsetX,
            [nameof(FootOffsetY)] = FootOffsetY,
            [nameof(FootKeysByStyle)] = WriteFootKeys(FootKeysByStyle),
            [nameof(FootKeysTextByStyle)] = WriteFootLabels(FootKeysTextByStyle),
            [nameof(Counts)] = counts,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        ShowOutsideGame = IOUtils.Read(token, nameof(ShowOutsideGame), ShowOutsideGame);
        Mode = NormalizeMode(IOUtils.Read(token, nameof(Mode), Mode));
        Style = Mathf.Clamp(IOUtils.Read(token, nameof(Style), Style), 0, MaxStyle);
        Size = IOUtils.Read(token, nameof(Size), Size);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
        SyncToKeyLimiter = IOUtils.Read(token, nameof(SyncToKeyLimiter), SyncToKeyLimiter);
        RainEnabled = IOUtils.Read(token, nameof(RainEnabled), RainEnabled);
        RainSpeed = IOUtils.Read(token, nameof(RainSpeed), RainSpeed);
        RainHeight = IOUtils.Read(token, nameof(RainHeight), RainHeight);
        RainFade = IOUtils.Read(token, nameof(RainFade), RainFade);
        RainWidth = IOUtils.Read(token, nameof(RainWidth), RainWidth);
        Rain2Width = IOUtils.Read(token, nameof(Rain2Width), Rain2Width);
        RainOffsetY = IOUtils.Read(token, nameof(RainOffsetY), RainOffsetY);
        Rain2OffsetY = IOUtils.Read(token, nameof(Rain2OffsetY), Rain2OffsetY);
        StatsTogether = IOUtils.Read(token, nameof(StatsTogether), StatsTogether);
        CountFormatting = IOUtils.Read(token, nameof(CountFormatting), CountFormatting);
        HideMainKeyCount = IOUtils.Read(token, nameof(HideMainKeyCount), HideMainKeyCount);
        PerKeyKps = IOUtils.Read(token, nameof(PerKeyKps), PerKeyKps);
        StreamerMode = IOUtils.Read(token, nameof(StreamerMode), StreamerMode);
        RainR = IOUtils.Read(token, nameof(RainR), RainR);
        RainG = IOUtils.Read(token, nameof(RainG), RainG);
        RainB = IOUtils.Read(token, nameof(RainB), RainB);
        RainA = IOUtils.Read(token, nameof(RainA), RainA);
        Rain2R = IOUtils.Read(token, nameof(Rain2R), Rain2R);
        Rain2G = IOUtils.Read(token, nameof(Rain2G), Rain2G);
        Rain2B = IOUtils.Read(token, nameof(Rain2B), Rain2B);
        Rain2A = IOUtils.Read(token, nameof(Rain2A), Rain2A);
        Rain3R = IOUtils.Read(token, nameof(Rain3R), Rain3R);
        Rain3G = IOUtils.Read(token, nameof(Rain3G), Rain3G);
        Rain3B = IOUtils.Read(token, nameof(Rain3B), Rain3B);
        Rain3A = IOUtils.Read(token, nameof(Rain3A), Rain3A);
        string dmPreset = IOUtils.Read(token, nameof(DmPresetJson), DmPresetJson) ?? "";
        try {
            DmPresetJson = KeyViewerPersistence.SanitizeDmPreset(dmPreset);
        } catch {
            DmPresetJson = dmPreset;
        }
        DmSelectedTab = IOUtils.Read(token, nameof(DmSelectedTab), DmSelectedTab) ?? "4key";
        DmOffsetX = IOUtils.Read(token, nameof(DmOffsetX), DmOffsetX);
        DmOffsetY = IOUtils.Read(token, nameof(DmOffsetY), DmOffsetY);
        DmScale = Mathf.Clamp(IOUtils.Read(token, nameof(DmScale), DmScale), 0.2f, 4f);
        DmNoteEffect = IOUtils.Read(token, nameof(DmNoteEffect), DmNoteEffect);
        DmNoteSpeed = Mathf.Clamp(IOUtils.Read(token, nameof(DmNoteSpeed), DmNoteSpeed), 1f, 5000f);
        DmTrackHeight = Mathf.Clamp(IOUtils.Read(token, nameof(DmTrackHeight), DmTrackHeight), 0f, 5000f);
        DmNoteReverse = IOUtils.Read(token, nameof(DmNoteReverse), DmNoteReverse);
        DmShowCounter = IOUtils.Read(token, nameof(DmShowCounter), DmShowCounter);
        bool hasSingleFade = token[nameof(DmFadePx)] != null;
        DmFadePx = Mathf.Clamp(IOUtils.Read(token, nameof(DmFadePx), DmFadePx), 0f, 2000f);
        DmFadeTopPx = Mathf.Clamp(IOUtils.Read(token, nameof(DmFadeTopPx), DmFadeTopPx), 0f, 500f);
        if(!hasSingleFade && token[nameof(DmFadeTopPx)] != null) {
            DmFadePx = DmFadeTopPx;
        }
        DmFadeTopPx = DmFadePx;
        DmFadeBottomPx = 0f;
        DmReverseFadeTopPx = 0f;
        DmReverseFadeBottomPx = DmFadePx;
        DmDelayedNoteEnabled = IOUtils.Read(token, nameof(DmDelayedNoteEnabled), DmDelayedNoteEnabled);
        DmShortNoteThresholdMs = Mathf.Clamp(IOUtils.Read(token, nameof(DmShortNoteThresholdMs), DmShortNoteThresholdMs), 0f, 2000f);
        DmShortNoteMinLengthPx = Mathf.Clamp(IOUtils.Read(token, nameof(DmShortNoteMinLengthPx), DmShortNoteMinLengthPx), 1f, 9999f);
        DmKeyDisplayDelayMs = Mathf.Clamp(IOUtils.Read(token, nameof(DmKeyDisplayDelayMs), DmKeyDisplayDelayMs), 0f, 9999f);
        DmMinLitMs = Mathf.Clamp(IOUtils.Read(token, nameof(DmMinLitMs), DmMinLitMs), 0f, 500f);
        DmCssEnabled = IOUtils.Read(token, nameof(DmCssEnabled), DmCssEnabled);
        DmCssText = IOUtils.Read(token, nameof(DmCssText), DmCssText) ?? "";
        DmCssPath = IOUtils.Read(token, nameof(DmCssPath), DmCssPath) ?? "";
        Key10 = ReadKeys(token, nameof(Key10), Key10);
        Key12 = ReadKeys(token, nameof(Key12), Key12);
        Key16 = ReadKeys(token, nameof(Key16), Key16);
        Key20 = ReadKeys(token, nameof(Key20), Key20);
        Key8 = ReadKeys(token, nameof(Key8), Key8);
        Key14 = ReadKeys(token, nameof(Key14), Key14);
        Key10Text = ReadLabels(token, nameof(Key10Text), Key10Text);
        Key12Text = ReadLabels(token, nameof(Key12Text), Key12Text);
        Key16Text = ReadLabels(token, nameof(Key16Text), Key16Text);
        Key20Text = ReadLabels(token, nameof(Key20Text), Key20Text);
        Key8Text = ReadLabels(token, nameof(Key8Text), Key8Text);
        Key14Text = ReadLabels(token, nameof(Key14Text), Key14Text);
        IOUtils.ReadRgba(token, "Bg", ref BgR, ref BgG, ref BgB, ref BgA);
        IOUtils.ReadRgba(token, "BgPressed", ref BgPressedR, ref BgPressedG, ref BgPressedB, ref BgPressedA);
        IOUtils.ReadRgba(token, "Outline", ref OutlineR, ref OutlineG, ref OutlineB, ref OutlineA);
        IOUtils.ReadRgba(token, "OutlinePressed", ref OutlinePressedR, ref OutlinePressedG, ref OutlinePressedB, ref OutlinePressedA);
        IOUtils.ReadRgba(token, "Text", ref TextR, ref TextG, ref TextB, ref TextA);
        IOUtils.ReadRgba(token, "TextPressed", ref TextPressedR, ref TextPressedG, ref TextPressedB, ref TextPressedA);
        KeyFontScale = IOUtils.Read(token, nameof(KeyFontScale), KeyFontScale);
        CounterFontScale = IOUtils.Read(token, nameof(CounterFontScale), CounterFontScale);
        PerKeyKeyFont = ReadFloats(token, nameof(PerKeyKeyFont), PerKeyKeyFont);
        PerKeyCounterFont = ReadFloats(token, nameof(PerKeyCounterFont), PerKeyCounterFont);
        PerKeyFontEnabled = ReadBools(token, nameof(PerKeyFontEnabled),
            Filled(SlotCount, IOUtils.Read(token, "PerKeyFontSizes", false)));
        PerKeyFontInit = ReadBools(token, nameof(PerKeyFontInit),
            Filled(SlotCount, IOUtils.Read(token, "PerKeyFontInitialized", false)));
        PerKeyColorEnabled = ReadBools(token, nameof(PerKeyColorEnabled),
            Filled(SlotCount, IOUtils.Read(token, "PerKeyColors", false)));
        PerKeyColorInit = ReadBools(token, nameof(PerKeyColorInit),
            Filled(SlotCount, IOUtils.Read(token, "PerKeyColorsInitialized", false)));
        PerKeyBg = ReadColors(token, nameof(PerKeyBg), PerKeyBg);
        PerKeyBgPressed = ReadColors(token, nameof(PerKeyBgPressed), PerKeyBgPressed);
        PerKeyOutline = ReadColors(token, nameof(PerKeyOutline), PerKeyOutline);
        PerKeyOutlinePressed = ReadColors(token, nameof(PerKeyOutlinePressed), PerKeyOutlinePressed);
        PerKeyText = ReadColors(token, nameof(PerKeyText), PerKeyText);
        PerKeyTextPressed = ReadColors(token, nameof(PerKeyTextPressed), PerKeyTextPressed);
        PerKeyRain = ReadColors(token, nameof(PerKeyRain), PerKeyRain);
        IOUtils.ReadRgba(token, "GhostRain", ref GhostRainR, ref GhostRainG, ref GhostRainB, ref GhostRainA);
        GhostRainDotted = IOUtils.Read(token, nameof(GhostRainDotted), GhostRainDotted);
        GhostRainDotLength = IOUtils.Read(token, nameof(GhostRainDotLength), GhostRainDotLength);
        GhostRainGapLength = IOUtils.Read(token, nameof(GhostRainGapLength), GhostRainGapLength);
        GhostKey8 = ReadKeys(token, nameof(GhostKey8), GhostKey8);
        GhostKey10 = ReadKeys(token, nameof(GhostKey10), GhostKey10);
        GhostKey12 = ReadKeys(token, nameof(GhostKey12), GhostKey12);
        GhostKey14 = ReadKeys(token, nameof(GhostKey14), GhostKey14);
        GhostKey16 = ReadKeys(token, nameof(GhostKey16), GhostKey16);
        GhostKey20 = ReadKeys(token, nameof(GhostKey20), GhostKey20);
        FootStyle = Mathf.Clamp(IOUtils.Read(token, nameof(FootStyle), FootStyle), 0, MaxFootStyle);
        FootOffsetX = IOUtils.Read(token, nameof(FootOffsetX), FootOffsetX);
        FootOffsetY = IOUtils.Read(token, nameof(FootOffsetY), FootOffsetY);
        ReadFootKeys(token);
        Counts.Clear();
        if(token[nameof(Counts)] is JObject counts) {
            foreach(var prop in counts.Properties()) {
                try {
                    Counts[prop.Name] = prop.Value.Value<int>();
                } catch {
                }
            }
        }
    }
    public static string NormalizeMode(string mode) =>
        string.Equals(mode, KvMigrationPlan.LegacyModeDmNote, StringComparison.OrdinalIgnoreCase) ? KvMigrationPlan.LegacyModeDmNote
        : string.Equals(mode, KvMigrationPlan.LegacyModeSimple, StringComparison.OrdinalIgnoreCase) ? KvMigrationPlan.LegacyModeSimple
        : ModeEditor;
    private static JArray WriteLabels(string[] labels) {
        JArray arr = [];
        foreach(string label in labels) arr.Add(label ?? "");
        return arr;
    }
    private static JArray WriteFootKeys(int[][] byStyle) {
        JArray outer = [];
        foreach(int[] arr in byStyle) outer.Add(new JArray(arr));
        return outer;
    }
    private static JArray WriteFootLabels(string[][] byStyle) {
        JArray outer = [];
        foreach(string[] arr in byStyle) outer.Add(WriteLabels(arr));
        return outer;
    }
    private void ReadFootKeys(JToken token) {
        if(token[nameof(FootKeysByStyle)] is JArray keysOuter && keysOuter.Count == FootKeysByStyle.Length) {
            for(int s = 0; s < FootKeysByStyle.Length; s++) {
                if(keysOuter[s] is not JArray arr || arr.Count != FootKeysByStyle[s].Length) continue;
                try { for(int i = 0; i < arr.Count; i++) FootKeysByStyle[s][i] = arr[i].Value<int>(); } catch { }
            }
        } else if(token["FootKeys"] is JArray legacyKeys) {
            SeedFootFromFlat(legacyKeys);
        }
        if(token[nameof(FootKeysTextByStyle)] is JArray textOuter && textOuter.Count == FootKeysTextByStyle.Length) {
            for(int s = 0; s < FootKeysTextByStyle.Length; s++) {
                if(textOuter[s] is not JArray arr || arr.Count != FootKeysTextByStyle[s].Length) continue;
                for(int i = 0; i < arr.Count; i++) FootKeysTextByStyle[s][i] = arr[i].Type == JTokenType.String ? arr[i].ToString() : "";
            }
        } else if(token["FootKeysText"] is JArray legacyText) {
            SeedFootLabelsFromFlat(legacyText);
        }
    }
    private void SeedFootFromFlat(JArray flat) {
        int[] values = new int[flat.Count];
        try { for(int i = 0; i < values.Length; i++) values[i] = flat[i].Value<int>(); } catch { return; }
        foreach(int[] dest in FootKeysByStyle) {
            int n = Mathf.Min(dest.Length, values.Length);
            for(int i = 0; i < n; i++) dest[i] = values[i];
        }
    }
    private void SeedFootLabelsFromFlat(JArray flat) {
        string[] values = new string[flat.Count];
        for(int i = 0; i < values.Length; i++) values[i] = flat[i].Type == JTokenType.String ? flat[i].ToString() : "";
        foreach(string[] dest in FootKeysTextByStyle) {
            int n = Mathf.Min(dest.Length, values.Length);
            for(int i = 0; i < n; i++) dest[i] = values[i];
        }
    }
    private static JArray WriteColors(Color[] colors) {
        JArray arr = [];
        foreach(Color c in colors) {
            arr.Add(c.r);
            arr.Add(c.g);
            arr.Add(c.b);
            arr.Add(c.a);
        }
        return arr;
    }
    private static Color[] ReadColors(JToken token, string name, Color[] fallback) {
        if(token[name] is not JArray arr || arr.Count != fallback.Length * 4) return fallback;
        try {
            Color[] result = new Color[fallback.Length];
            for(int i = 0; i < result.Length; i++) {
                int b = i * 4;
                result[i] = new Color(
                    arr[b].Value<float>(), arr[b + 1].Value<float>(),
                    arr[b + 2].Value<float>(), arr[b + 3].Value<float>());
            }
            return result;
        } catch {
            return fallback;
        }
    }
    private static float[] ReadFloats(JToken token, string name, float[] fallback) =>
        ReadArray(token, name, fallback, t => t.Value<float>());
    private static bool[] ReadBools(JToken token, string name, bool[] fallback) =>
        ReadArray(token, name, fallback, t => t.Value<bool>());
    private static string[] ReadLabels(JToken token, string name, string[] fallback) {
        if(token[name] is not JArray arr || arr.Count != fallback.Length) return fallback;
        string[] result = new string[arr.Count];
        for(int i = 0; i < arr.Count; i++) result[i] = arr[i].Type == JTokenType.String ? arr[i].ToString() : "";
        return result;
    }
    private static int[] ReadKeys(JToken token, string name, int[] fallback) =>
        ReadArray(token, name, fallback, t => t.Value<int>());
    private static T[] ReadArray<T>(JToken token, string name, T[] fallback, Func<JToken, T> read) {
        if(token[name] is not JArray arr || arr.Count != fallback.Length) return fallback;
        try {
            T[] result = new T[arr.Count];
            for(int i = 0; i < arr.Count; i++) result[i] = read(arr[i]);
            return result;
        } catch {
            return fallback;
        }
    }
}
