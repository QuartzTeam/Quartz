using Newtonsoft.Json.Linq;
using Quartz.Features.KeyViewer.Layout;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerSettings : ISettingsFile {
    public const string ModeEditor = "editor";
    public bool Enabled = true;
    public bool ShowOutsideGame = true;
    public string Mode = KvMigrationPlan.LegacyModeSimple;
    public int Style = 2;
    public const int MaxStyle = 5;
    public float Size = 0.8f;
    public float OffsetX = -713.51886f;
    public float OffsetY = 24.76001f;
    public bool SyncToKeyLimiter = true;
    public bool RainEnabled = true;
    public float RainSpeed = 450f;
    public float RainHeight = 300f;
    public float RainFade = 60f;
    public float RainWidth = 0f;
    public float Rain2Width = 40f;
    public float RainOffsetY = 0f;
    public float Rain2OffsetY = 0f;
    public bool StatsTogether = true;
    public bool CountFormatting = false;
    public bool HideMainKeyCount = false;
    public bool PerKeyKps = false;
    public bool StreamerMode = false;
    public float RainR = 1f, RainG = 0f, RainB = 0f, RainA = 1f;
    public float Rain2R = 1f, Rain2G = 1f, Rain2B = 1f, Rain2A = 1f;
    public float Rain3R = 1f, Rain3G = 0f, Rain3B = 1f, Rain3A = 1f;
    public string DmPresetJson = "";
    public string DmSelectedTab = "4key";
    public float DmOffsetX = 0f;
    public float DmOffsetY = 240f;
    public float DmScale = 1f;
    public bool DmNoteEffect = true;
    public float DmNoteSpeed = 1000f;
    public float DmTrackHeight = 200f;
    public bool DmNoteReverse = false;
    public bool DmShowCounter = true;
    public float DmFadePx = 60f;
    public float DmFadeTopPx = 60f;
    public float DmFadeBottomPx = 0f;
    public float DmReverseFadeTopPx = 0f;
    public float DmReverseFadeBottomPx = 60f;
    public bool DmDelayedNoteEnabled = false;
    public float DmShortNoteThresholdMs = 50f;
    public float DmShortNoteMinLengthPx = 30f;
    public float DmKeyDisplayDelayMs = 0f;
    public float DmMinLitMs = 40f;
    public bool IndependentInput = true;
    public bool DmCssEnabled = false;
    public string DmCssText = "";
    public string DmCssPath = "";
    public int[] Key10 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 104];
    public int[] Key12 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46];
    public int[] Key16 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46, 97, 304, 273, 13];
    public int[] Key20 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 44, 97, 304, 303, 13, 110, 103, 109, 107];
    public int[] Key8 = [113, 51, 52, 116, 111, 45, 61, 92];
    public int[] Key14 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46, 97, 304];
    public string[] Key10Text = new string[10];
    public string[] Key12Text = new string[12];
    public string[] Key16Text = new string[16];
    public string[] Key20Text = new string[20];
    public string[] Key8Text = new string[8];
    public string[] Key14Text = new string[14];
    public float BgR = 1f, BgG = 0.2352941f, BgB = 0.2352941f, BgA = 0.1960784f;
    public float BgPressedR = 1f, BgPressedG = 1f, BgPressedB = 1f, BgPressedA = 1f;
    public float OutlineR = 1f, OutlineG = 0f, OutlineB = 0f, OutlineA = 1f;
    public float OutlinePressedR = 1f, OutlinePressedG = 1f, OutlinePressedB = 1f, OutlinePressedA = 1f;
    public float TextR = 1f, TextG = 1f, TextB = 1f, TextA = 1f;
    public float TextPressedR = 0f, TextPressedG = 0f, TextPressedB = 0f, TextPressedA = 1f;
    public const int SlotCount = 36;
    public const int FootSlotBase = 20;
    public float KeyFontScale = 1f;
    public float CounterFontScale = 1f;
    public bool[] PerKeyFontEnabled = new bool[SlotCount];
    public bool[] PerKeyFontInit = new bool[SlotCount];
    public float[] PerKeyKeyFont = Filled(SlotCount, 1f);
    public float[] PerKeyCounterFont = Filled(SlotCount, 1f);
    public bool[] PerKeyColorEnabled = new bool[SlotCount];
    public bool[] PerKeyColorInit = new bool[SlotCount];
    public Color[] PerKeyBg = FilledColor(SlotCount, new Color(1f, 0.2352941f, 0.2352941f, 0.1960784f));
    public Color[] PerKeyBgPressed = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyOutline = FilledColor(SlotCount, new Color(1f, 0f, 0f, 1f));
    public Color[] PerKeyOutlinePressed = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyText = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyTextPressed = FilledColor(SlotCount, new Color(0f, 0f, 0f, 1f));
    public Color[] PerKeyRain = FilledColor(SlotCount, new Color(1f, 0f, 0f, 1f));
    public int FootStyle = 0;
    public float FootOffsetX = -360f;
    public float FootOffsetY = 24.76001f;
    public const int MaxFootStyle = 8;
    private static readonly int[] FootKeyDefaults = [289, 285, 288, 284, 287, 283, 286, 282, 48, 54, 57, 53, 56, 52, 55, 51];
    public int[][] FootKeysByStyle = DefaultFootKeys();
    public string[][] FootKeysTextByStyle = DefaultFootLabels();
    public int FootKeyCount() => Mathf.Clamp(FootStyle, 0, MaxFootStyle) * 2;
    public int[] FootKeysForStyle(int footStyle) => FootKeysByStyle[Mathf.Clamp(footStyle, 0, MaxFootStyle)];
    public string[] FootLabelsForStyle(int footStyle) => FootKeysTextByStyle[Mathf.Clamp(footStyle, 0, MaxFootStyle)];
    private static int[][] DefaultFootKeys() {
        int[][] byStyle = new int[MaxFootStyle + 1][];
        for(int s = 0; s <= MaxFootStyle; s++) {
            int[] arr = new int[s * 2];
            Array.Copy(FootKeyDefaults, arr, arr.Length);
            byStyle[s] = arr;
        }
        return byStyle;
    }
    private static string[][] DefaultFootLabels() {
        string[][] byStyle = new string[MaxFootStyle + 1][];
        for(int s = 0; s <= MaxFootStyle; s++) byStyle[s] = new string[s * 2];
        return byStyle;
    }
    public float GhostRainR = 1f, GhostRainG = 0f, GhostRainB = 0f, GhostRainA = 0.45f;
    public bool GhostRainDotted = false;
    public float GhostRainDotLength = 10f;
    public float GhostRainGapLength = 6f;
    public int[] GhostKey8 = new int[8];
    public int[] GhostKey10 = new int[10];
    public int[] GhostKey12 = new int[12];
    public int[] GhostKey14 = new int[14];
    public int[] GhostKey16 = new int[16];
    public int[] GhostKey20 = new int[20];
    public Dictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase);
    public int[] KeysForStyle(int style) => style switch {
        0 => Key10,
        1 => Key12,
        3 => Key20,
        4 => Key8,
        5 => Key14,
        _ => Key16,
    };
    public string[] LabelsForStyle(int style) => style switch {
        0 => Key10Text,
        1 => Key12Text,
        3 => Key20Text,
        4 => Key8Text,
        5 => Key14Text,
        _ => Key16Text,
    };
    public int[] GhostKeysForStyle(int style) => style switch {
        0 => GhostKey10,
        1 => GhostKey12,
        3 => GhostKey20,
        4 => GhostKey8,
        5 => GhostKey14,
        _ => GhostKey16,
    };
    public Color GetGhostRain() => IOUtils.Rgba(GhostRainR, GhostRainG, GhostRainB, GhostRainA);
    public void SetGhostRain(Color c) => IOUtils.SetRgba(c, ref GhostRainR, ref GhostRainG, ref GhostRainB, ref GhostRainA);
    public Color GetBg() => IOUtils.Rgba(BgR, BgG, BgB, BgA);
    public void SetBg(Color c) => IOUtils.SetRgba(c, ref BgR, ref BgG, ref BgB, ref BgA);
    public Color GetBgPressed() => IOUtils.Rgba(BgPressedR, BgPressedG, BgPressedB, BgPressedA);
    public void SetBgPressed(Color c) => IOUtils.SetRgba(c, ref BgPressedR, ref BgPressedG, ref BgPressedB, ref BgPressedA);
    public Color GetOutline() => IOUtils.Rgba(OutlineR, OutlineG, OutlineB, OutlineA);
    public void SetOutline(Color c) => IOUtils.SetRgba(c, ref OutlineR, ref OutlineG, ref OutlineB, ref OutlineA);
    public Color GetOutlinePressed() => IOUtils.Rgba(OutlinePressedR, OutlinePressedG, OutlinePressedB, OutlinePressedA);
    public void SetOutlinePressed(Color c) => IOUtils.SetRgba(c, ref OutlinePressedR, ref OutlinePressedG, ref OutlinePressedB, ref OutlinePressedA);
    public Color GetText() => IOUtils.Rgba(TextR, TextG, TextB, TextA);
    public void SetText(Color c) => IOUtils.SetRgba(c, ref TextR, ref TextG, ref TextB, ref TextA);
    public Color GetTextPressed() => IOUtils.Rgba(TextPressedR, TextPressedG, TextPressedB, TextPressedA);
    public void SetTextPressed(Color c) => IOUtils.SetRgba(c, ref TextPressedR, ref TextPressedG, ref TextPressedB, ref TextPressedA);
    public Color GetRain() => IOUtils.Rgba(RainR, RainG, RainB, RainA);
    public void SetRain(Color c) => IOUtils.SetRgba(c, ref RainR, ref RainG, ref RainB, ref RainA);
    public Color GetRain2() => IOUtils.Rgba(Rain2R, Rain2G, Rain2B, Rain2A);
    public void SetRain2(Color c) => IOUtils.SetRgba(c, ref Rain2R, ref Rain2G, ref Rain2B, ref Rain2A);
    public Color GetRain3() => IOUtils.Rgba(Rain3R, Rain3G, Rain3B, Rain3A);
    public void SetRain3(Color c) => IOUtils.SetRgba(c, ref Rain3R, ref Rain3G, ref Rain3B, ref Rain3A);
    public Color PerKeyOr(Color[] arr, int slot, Color global) =>
        arr != null && slot >= 0 && slot < arr.Length
        && slot < PerKeyColorEnabled.Length && PerKeyColorEnabled[slot]
            ? arr[slot] : global;
    public float KeyFontFor(int slot) =>
        slot >= 0 && slot < PerKeyKeyFont.Length
        && slot < PerKeyFontEnabled.Length && PerKeyFontEnabled[slot]
            ? PerKeyKeyFont[slot] : KeyFontScale;
    public float CounterFontFor(int slot) =>
        slot >= 0 && slot < PerKeyCounterFont.Length
        && slot < PerKeyFontEnabled.Length && PerKeyFontEnabled[slot]
            ? PerKeyCounterFont[slot] : CounterFontScale;
    public void SeedPerKeyColorsFromGlobal() {
        for(int i = 0; i < SlotCount; i++) SeedPerKeyColorsFromGlobal(i);
    }
    public void SeedPerKeyColorsFromGlobal(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        PerKeyBg[slot] = GetBg();
        PerKeyBgPressed[slot] = GetBgPressed();
        PerKeyOutline[slot] = GetOutline();
        PerKeyOutlinePressed[slot] = GetOutlinePressed();
        PerKeyText[slot] = GetText();
        PerKeyTextPressed[slot] = GetTextPressed();
        PerKeyRain[slot] = GetRain();
    }
    public void SeedPerKeyFontFromGlobal() {
        for(int i = 0; i < SlotCount; i++) SeedPerKeyFontFromGlobal(i);
    }
    public void SeedPerKeyFontFromGlobal(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        PerKeyKeyFont[slot] = KeyFontScale;
        PerKeyCounterFont[slot] = CounterFontScale;
    }
    public void CopyPerKeyColorsToAll(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        Color bg = PerKeyBg[slot], bgP = PerKeyBgPressed[slot];
        Color ol = PerKeyOutline[slot], olP = PerKeyOutlinePressed[slot];
        Color tx = PerKeyText[slot], txP = PerKeyTextPressed[slot];
        Color rn = PerKeyRain[slot];
        for(int i = 0; i < SlotCount; i++) {
            PerKeyBg[i] = bg;
            PerKeyBgPressed[i] = bgP;
            PerKeyOutline[i] = ol;
            PerKeyOutlinePressed[i] = olP;
            PerKeyText[i] = tx;
            PerKeyTextPressed[i] = txP;
            PerKeyRain[i] = rn;
            PerKeyColorEnabled[i] = true;
            PerKeyColorInit[i] = true;
        }
    }
    private static float[] Filled(int n, float v) {
        float[] a = new float[n];
        for(int i = 0; i < n; i++) a[i] = v;
        return a;
    }
    private static bool[] Filled(int n, bool v) {
        bool[] a = new bool[n];
        for(int i = 0; i < n; i++) a[i] = v;
        return a;
    }
    private static Color[] FilledColor(int n, Color c) {
        Color[] a = new Color[n];
        for(int i = 0; i < n; i++) a[i] = c;
        return a;
    }
    public int GetCount(string key) =>
        key != null && Counts.TryGetValue(key, out int v) ? v : 0;
    public void SetCount(string key, int value) {
        if(!string.IsNullOrEmpty(key)) Counts[key] = value;
    }
}
