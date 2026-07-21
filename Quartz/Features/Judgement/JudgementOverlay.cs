using System.Globalization;
using System.Text;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Status;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.Features.Judgement;
public static class JudgementOverlay {
    public static SettingsFile<JudgementSettings> ConfMgr { get; private set; }
    public static JudgementSettings Conf => ConfMgr.Data;
    private const float BaseFontSize = 38f;
    private const float BottomMargin = 6.5f;
    private static GameObject canvasObj;
    private static GraphicRaycaster raycaster;
    private static RectTransform root;
    private static HorizontalLayoutGroup rowLayout;
    private static readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[Judgement.Slots];
    private static TextMeshProUGUI rowLabel;
    private static bool compact;
    private static GameObject dragObj;
    private static Updater updater;
    private const int PerfectSlot = 4;
    private static TextMeshProUGUI xPlusLabel;
    private static TextMeshProUGUI xMinusLabel;
    private static readonly Color XPerfectColor = new(0.30f, 0.80f, 1f, 1f);
    private static readonly Color PlusMinusPerfectColor = new(0.38f, 1f, 0.31f, 1f);
    private static readonly string[] SlotHex =
        System.Array.ConvertAll(Judgement.SlotColors, ColorUtility.ToHtmlStringRGB);
    private static readonly string XPerfectHex = ColorUtility.ToHtmlStringRGB(XPerfectColor);
    private static readonly string PlusMinusHex = ColorUtility.ToHtmlStringRGB(PlusMinusPerfectColor);
    private static readonly StringBuilder rowBuilder = new(160);
    public static void EnsureConf() => ConfMgr ??= SettingsFile<JudgementSettings>.Loaded("Judgement.json");
    public static void Initialize(GameObject rootObject) {
        if(canvasObj != null) return;
        EnsureConf();
        compact = Conf.CompactRow;
        canvasObj = UnityUtils.CreateOverlayCanvas("QuartzJudgementCanvas", rootObject.transform, 32756, out raycaster);
        GameObject rowObj = new("JudgementRow");
        rowObj.transform.SetParent(canvasObj.transform, false);
        root = rowObj.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        if(compact) BuildCompactRow(rowObj);
        else BuildMultiLabelRow(rowObj);
        dragObj = ReorganizeHandle.CreateDragSurface(root, () => MainCore.Tr.Get("JUDGEMENT", "Judgement"), Save, ignoreLayout: true);
        updater = canvasObj.AddComponent<Updater>();
        Apply();
    }
    private static void BuildMultiLabelRow(GameObject rowObj) {
        rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        ContentSizeFitter fit = rowObj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for(int i = 0; i < labels.Length; i++) {
            GameObject obj = new("Judgement_" + i);
            obj.transform.SetParent(rowObj.transform, false);
            obj.AddComponent<RectTransform>();
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.font = FontManager.Current;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Judgement.SlotColors[i];
            text.raycastTarget = false;
            TextCompat.NoWrap(text);
            text.text = "0";
            labels[i] = text;
        }
        xPlusLabel = CreateJudgementLabel("Judgement_XPlus", PlusMinusPerfectColor);
        xMinusLabel = CreateJudgementLabel("Judgement_XMinus", PlusMinusPerfectColor);
        xPlusLabel.transform.SetSiblingIndex(PerfectSlot);
        xMinusLabel.transform.SetSiblingIndex(PerfectSlot + 2);
        xPlusLabel.gameObject.SetActive(false);
        xMinusLabel.gameObject.SetActive(false);
    }
    private static void BuildCompactRow(GameObject rowObj) {
        HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fit = rowObj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        GameObject labelObj = new("RowLabel");
        labelObj.transform.SetParent(rowObj.transform, false);
        labelObj.AddComponent<RectTransform>();
        rowLabel = labelObj.AddComponent<TextMeshProUGUI>();
        rowLabel.font = FontManager.Current;
        rowLabel.alignment = TextAlignmentOptions.Center;
        rowLabel.richText = true;
        rowLabel.raycastTarget = false;
        TextCompat.NoWrap(rowLabel);
        rowLabel.text = "0";
    }
    public static void Apply() {
        if(root == null) return;
        root.anchoredPosition = OverlayCalibration.Scale(new Vector2(Conf.OffsetX, BottomMargin + Conf.OffsetY));
        float fontSize = FontSize();
        if(compact) {
            ApplyTextStyle(rowLabel, fontSize);
            return;
        }
        rowLayout.spacing = RowSpacing();
        foreach(TextMeshProUGUI label in labels) ApplyTextStyle(label, fontSize);
        ApplyTextStyle(xPlusLabel, fontSize);
        ApplyTextStyle(xMinusLabel, fontSize);
    }
    private static float FontSize() => BaseFontSize * Mathf.Clamp(Conf.Size, 0.3f, 3f);
    private static float RowSpacing() => Mathf.Max(3f, FontSize() * 0.07f) + Mathf.Clamp(Conf.Spacing, -20f, 80f);
    private static void ApplyTextStyle(TextMeshProUGUI label, float fontSize) {
        if(label == null) return;
        label.fontSize = fontSize;
        TMPTextShadow.Apply(
            label,
            Conf.TextShadowEnabled,
            Conf.TextShadowX,
            Conf.TextShadowY,
            Conf.TextShadowSoftness,
            Conf.GetTextShadowColor()
        );
    }
    private static TextMeshProUGUI CreateJudgementLabel(string name, Color color) {
        GameObject obj = new(name);
        obj.transform.SetParent(root, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = FontManager.Current;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        TextCompat.NoWrap(text);
        text.text = "0";
        return text;
    }
    public static void Save() => ConfMgr?.Save();
    public static void ResetPosition() {
        JudgementSettings def = new();
        Conf.OffsetX = def.OffsetX;
        Conf.OffsetY = def.OffsetY;
        Apply();
        Save();
    }
    public static void Dispose() {
        if(canvasObj == null) return;
        ConfMgr?.Save();
        Object.Destroy(canvasObj);
        canvasObj = null;
        raycaster = null;
        root = null;
        rowLayout = null;
        rowLabel = null;
        System.Array.Clear(labels, 0, labels.Length);
        xPlusLabel = null;
        xMinusLabel = null;
        dragObj = null;
        updater = null;
    }
    private sealed class Updater : MonoBehaviour {
        private readonly int[] cached = new int[Judgement.Slots];
        private int cachedPlus = -1;
        private int cachedMinus = -1;
        private bool lastXpMode;
        private bool cacheValid;
        private float lastFontSize = float.NaN;
        private TMP_FontAsset lastFont;
        private float lastRowSpacing = float.NaN;
        private string lastGap;
        private float lastGapSpacing = float.NaN;
        private void Update() {
            if(root == null) return;
            bool isReorganizing = UICore.IsReorganizing;
            bool show = (Panels.PanelsOverlay.IsEnabled && Conf.Enabled && GameStats.InGame) || isReorganizing;
            if(raycaster != null && raycaster.enabled != isReorganizing) raycaster.enabled = isReorganizing;
            if(root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if(dragObj != null && dragObj.activeSelf != isReorganizing) dragObj.SetActive(isReorganizing);
            if(!show) return;
            if(isReorganizing) {
                Vector2 stored = OverlayCalibration.Unscale(root.anchoredPosition);
                Conf.OffsetX = stored.x;
                Conf.OffsetY = stored.y - BottomMargin;
            }
            TMP_FontAsset font = FontManager.Current;
            float fontSize = FontSize();
            if(compact) UpdateCompact(font, fontSize);
            else UpdateMultiLabel(font, fontSize);
        }
        private void UpdateMultiLabel(TMP_FontAsset font, float fontSize) {
            rowLayout.spacing = RowSpacing();
            bool xpMode = Conf.ShowXPerfect && XPerfectBridge.Active;
            bool xpModeChanged = xpMode != lastXpMode;
            bool changed = !cacheValid || fontSize != lastFontSize || font != lastFont || xpModeChanged;
            for(int i = 0; i < labels.Length; i++) {
                TextMeshProUGUI label = labels[i];
                if(label.font != font) label.font = font;
                if(label.fontSize != fontSize) label.fontSize = fontSize;
                int count = i == PerfectSlot && xpMode ? XPerfectBridge.XCount() : Judgement.SlotCount(i);
                if(!cacheValid || count != cached[i] || xpModeChanged) {
                    cached[i] = count;
                    UnityUtils.SetCount(label, count);
                    changed = true;
                }
            }
            UpdateXPerfectLabels(xpMode, xpModeChanged, font, fontSize, ref changed);
            if(xpModeChanged) labels[PerfectSlot].color = xpMode ? XPerfectColor : Judgement.SlotColors[PerfectSlot];
            cacheValid = true;
            lastFontSize = fontSize;
            lastFont = font;
            lastXpMode = xpMode;
            if(changed) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                for(int i = 0; i < labels.Length; i++) ApplyTextStyle(labels[i], fontSize);
                if(xpMode) {
                    ApplyTextStyle(xPlusLabel, fontSize);
                    ApplyTextStyle(xMinusLabel, fontSize);
                }
            }
        }
        private void UpdateCompact(TMP_FontAsset font, float fontSize) {
            if(rowLabel.font != font) rowLabel.font = font;
            if(rowLabel.fontSize != fontSize) rowLabel.fontSize = fontSize;
            float rowSpacing = RowSpacing();
            bool xpMode = Conf.ShowXPerfect && XPerfectBridge.Active;
            bool xpModeChanged = xpMode != lastXpMode;
            bool changed = !cacheValid || fontSize != lastFontSize || font != lastFont
                || xpModeChanged || rowSpacing != lastRowSpacing;
            for(int i = 0; i < Judgement.Slots; i++) {
                int count = i == PerfectSlot && xpMode ? XPerfectBridge.XCount() : Judgement.SlotCount(i);
                if(!cacheValid || count != cached[i] || xpModeChanged) {
                    cached[i] = count;
                    changed = true;
                }
            }
            if(xpMode) {
                int plus = XPerfectBridge.PlusCount();
                if(!cacheValid || plus != cachedPlus || xpModeChanged) {
                    cachedPlus = plus;
                    changed = true;
                }
                int minus = XPerfectBridge.MinusCount();
                if(!cacheValid || minus != cachedMinus || xpModeChanged) {
                    cachedMinus = minus;
                    changed = true;
                }
            }
            cacheValid = true;
            lastFontSize = fontSize;
            lastFont = font;
            lastXpMode = xpMode;
            lastRowSpacing = rowSpacing;
            if(!changed) return;
            StringBuilder sb = rowBuilder;
            sb.Clear();
            if(lastGap == null || rowSpacing != lastGapSpacing) {
                lastGap = rowSpacing.ToString("0.##", CultureInfo.InvariantCulture);
                lastGapSpacing = rowSpacing;
            }
            string gap = lastGap;
            for(int i = 0; i < Judgement.Slots; i++) {
                if(i > 0) sb.Append("<space=").Append(gap).Append("px>");
                if(i == PerfectSlot && xpMode) {
                    AppendCount(sb, PlusMinusHex, cachedPlus);
                    sb.Append("<space=").Append(gap).Append("px>");
                    AppendCount(sb, XPerfectHex, cached[i]);
                    sb.Append("<space=").Append(gap).Append("px>");
                    AppendCount(sb, PlusMinusHex, cachedMinus);
                } else {
                    AppendCount(sb, SlotHex[i], cached[i]);
                }
            }
            rowLabel.text = sb.ToString();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            ApplyTextStyle(rowLabel, fontSize);
        }
        private static void AppendCount(StringBuilder sb, string hex, int count) {
            sb.Append("<color=#").Append(hex).Append('>').Append(count).Append("</color>");
        }
        private void UpdateXPerfectLabels(
            bool xpMode, bool xpModeChanged, TMP_FontAsset font, float fontSize, ref bool changed
        ) {
            if(xPlusLabel == null || xMinusLabel == null) return;
            if(xPlusLabel.gameObject.activeSelf != xpMode) {
                xPlusLabel.gameObject.SetActive(xpMode);
                xMinusLabel.gameObject.SetActive(xpMode);
                changed = true;
            }
            if(!xpMode) return;
            if(xPlusLabel.font != font) xPlusLabel.font = font;
            if(xMinusLabel.font != font) xMinusLabel.font = font;
            if(xPlusLabel.fontSize != fontSize) xPlusLabel.fontSize = fontSize;
            if(xMinusLabel.fontSize != fontSize) xMinusLabel.fontSize = fontSize;
            int plus = XPerfectBridge.PlusCount();
            if(!cacheValid || plus != cachedPlus || xpModeChanged) {
                cachedPlus = plus;
                UnityUtils.SetCount(xPlusLabel, plus);
                changed = true;
            }
            int minus = XPerfectBridge.MinusCount();
            if(!cacheValid || minus != cachedMinus || xpModeChanged) {
                cachedMinus = minus;
                UnityUtils.SetCount(xMinusLabel, minus);
                changed = true;
            }
        }
    }
}
