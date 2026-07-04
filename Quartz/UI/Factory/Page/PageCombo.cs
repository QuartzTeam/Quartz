using Quartz.Core;
using Quartz.Features.Combo;
using Quartz.Features.Interop;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

// Combo settings section for the Overlay tab. The layout (center value with a
// caption beneath, below the progress bar) is the original combo; the Label /
// Count / Animation / Color groups expose the extra knobs ported from the
// combo-progressbar-playcount branch. Defaults reproduce the original look.
internal static class PageCombo {
    public static void AppendTo(Transform content) {
        ComboOverlay.EnsureConf();
        ComboSettings conf = ComboOverlay.Conf;
        ComboSettings def = new();

        void Save() => ComboOverlay.Save();
        void Apply() => ComboOverlay.Apply();
        void ApplyCaptionShadow() => ComboOverlay.ApplyCaptionShadow();
        void ApplyCountShadow() => ComboOverlay.ApplyCountShadow();

        TextMeshProUGUI headerLabel = null;
        void ApplyHeaderCue(bool enabled) {
            if(headerLabel != null) headerLabel.alpha = enabled ? 1f : 0.5f;
        }

        var sec = GenerateUI.Collapsible(
            content, "Combo", startExpanded: false,
            v => { conf.Enabled = v; Apply(); Save(); ApplyHeaderCue(v); },
            conf.Enabled
        );

        // Header gives no other indication of whether Combo is enabled without
        // expanding it, so dim the title when it's off.
        headerLabel = sec.HeaderObj.transform.Find("Bar/Label")?.GetComponent<TextMeshProUGUI>();
        ApplyHeaderCue(conf.Enabled);

        GenerateUI.ToggleTip(
            sec.Body,
            def.CountAuto,
            conf.CountAuto,
            v => { conf.CountAuto = v; Save(); },
            "Combo Counts Auto Hits",
            "combo_auto",
            "Count auto-hit judgements (e.g. holds/repeats) toward the combo, not just manual key presses."
        );

        // Only meaningful when the XPerfect mod is installed — Quartz reads the
        // X-perfect count FROM XPerfect, so with it disabled the toggle has no
        // data and would silently do nothing. Hidden unless installed, matching
        // the Show XPerfect toggle on PageJudgement.
        if(XPerfectBridge.Installed) {
            GenerateUI.ToggleTip(
                sec.Body,
                def.XPerfectComboEnabled,
                conf.XPerfectComboEnabled,
                v => { conf.XPerfectComboEnabled = v; Apply(); Save(); },
                "XPerfect Combo (X Only)",
                "combo_xperfect",
                "Count only dead-center X perfects toward the combo when the XPerfect mod is active. The caption becomes \"XCombo\"."
            );
        }

        GenerateUI.SnapSlider(sec.Body, "Font Size", "combo_fontsize",
            def.FontSize, 24f, 120f, conf.FontSize, "0 px", 1f,
            v => conf.FontSize = v, Apply, Save);

        GenerateUI.SnapSlider(sec.Body, "Master Size", "combo_master_size",
            def.MasterSize, 0.25f, 3f, conf.MasterSize, "0.00 x", 0.01f,
            v => conf.MasterSize = v, Apply, Save);

        // === Label ===
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_LABEL", "Label");

        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.ShowCaption,
            conf.ShowCaption,
            v => { conf.ShowCaption = v; Apply(); Save(); },
            "Show Caption",
            "combo_caption"
        );

        UIInput caption = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.CaptionText,
            conf.CaptionText,
            v => { conf.CaptionText = v; Apply(); Save(); },
            "Caption Label",
            MainCore.Spr.Get(UISprite.Text128),
            "combo_captiontext"
        );
        caption.InputField.characterLimit = 24;

        GenerateUI.SnapSlider(sec.Body, "Caption Size", "combo_caption_size",
            def.CaptionScale, 0.1f, 1.5f, conf.CaptionScale, "0.00 x", 0.01f,
            v => conf.CaptionScale = v, Apply, Save);

        GenerateUI.SnapSlider(sec.Body, "Caption Offset", "combo_captionoffset",
            def.CaptionOffsetY, -200f, 200f, conf.CaptionOffsetY, "0 px", 1f,
            v => conf.CaptionOffsetY = v, Apply, Save);

        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.CaptionShadowEnabled,
            conf.CaptionShadowEnabled,
            v => { conf.CaptionShadowEnabled = v; ApplyCaptionShadow(); Save(); },
            "Caption Shadow",
            "combo_caption_shadow_enabled"
        );

        GenerateUI.SnapSlider(sec.Body, "Caption Shadow X", "combo_caption_shadow_x",
            def.CaptionShadowX, -10f, 10f, conf.CaptionShadowX, "0.0 px", 0.1f,
            v => conf.CaptionShadowX = v, ApplyCaptionShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Caption Shadow Y", "combo_caption_shadow_y",
            def.CaptionShadowY, -10f, 10f, conf.CaptionShadowY, "0.0 px", 0.1f,
            v => conf.CaptionShadowY = v, ApplyCaptionShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Caption Shadow Softness", "combo_caption_shadow_softness",
            def.CaptionShadowSoftness, 0f, 20f, conf.CaptionShadowSoftness, "0.0 px", 0.1f,
            v => conf.CaptionShadowSoftness = v, ApplyCaptionShadow, Save);

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetCaptionShadowColor(),
            conf.GetCaptionShadowColor(),
            c => { conf.SetCaptionShadowColor(c); ApplyCaptionShadow(); },
            c => { conf.SetCaptionShadowColor(c); ApplyCaptionShadow(); Save(); },
            "Caption Shadow Color",
            "combo_caption_shadow_color"
        );

        // === Count ===
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_COUNT", "Count");

        GenerateUI.SnapSlider(sec.Body, "Thickness", "combo_count_thickness",
            def.CountThickness, -0.5f, 0.5f, conf.CountThickness, "0.00", 0.01f,
            v => conf.CountThickness = v, Apply, Save);

        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.CountShadowEnabled,
            conf.CountShadowEnabled,
            v => { conf.CountShadowEnabled = v; ApplyCountShadow(); Save(); },
            "Count Shadow",
            "combo_count_shadow_enabled"
        );

        GenerateUI.SnapSlider(sec.Body, "Count Shadow X", "combo_count_shadow_x",
            def.CountShadowX, -10f, 10f, conf.CountShadowX, "0.0 px", 0.1f,
            v => conf.CountShadowX = v, ApplyCountShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Count Shadow Y", "combo_count_shadow_y",
            def.CountShadowY, -10f, 10f, conf.CountShadowY, "0.0 px", 0.1f,
            v => conf.CountShadowY = v, ApplyCountShadow, Save);

        GenerateUI.SnapSlider(sec.Body, "Count Shadow Softness", "combo_count_shadow_softness",
            def.CountShadowSoftness, 0f, 20f, conf.CountShadowSoftness, "0.0 px", 0.1f,
            v => conf.CountShadowSoftness = v, ApplyCountShadow, Save);

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetCountShadowColor(),
            conf.GetCountShadowColor(),
            c => { conf.SetCountShadowColor(c); ApplyCountShadow(); },
            c => { conf.SetCountShadowColor(c); ApplyCountShadow(); Save(); },
            "Count Shadow Color",
            "combo_count_shadow_color"
        );

        // === Animation ===
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_ANIMATION", "Animation");

        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.NoPopAnim,
            conf.NoPopAnim,
            v => { conf.NoPopAnim = v; Save(); },
            "No Pop Animation",
            "combo_nopop"
        );

        GenerateUI.SnapSlider(sec.Body, "Pulse Duration", "combo_pulse_duration",
            def.PulseDuration, 0f, 1f, conf.PulseDuration, "0.00 s", 0.01f,
            v => conf.PulseDuration = v, null, Save);

        GenerateUI.SnapSlider(sec.Body, "Count Pulse Scale", "combo_pulse_count_scale",
            def.CountPulseScale, 0f, 1f, conf.CountPulseScale, "0.00 x", 0.01f,
            v => conf.CountPulseScale = v, null, Save);

        GenerateUI.SnapSlider(sec.Body, "Label Pulse Offset Y", "combo_pulse_label_offset",
            def.LabelPulseOffsetY, 0f, 60f, conf.LabelPulseOffsetY, "0 px", 1f,
            v => conf.LabelPulseOffsetY = v, null, Save);

        // === Color ===
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_COLOR", "Color");

        GenerateUI.SnapSlider(sec.Body, "Color Max Combo", "combo_colormax",
            def.ColorMax, 1f, 5000f, conf.ColorMax, "0", 1f,
            v => conf.ColorMax = Mathf.RoundToInt(v), null, Save);

        GenerateUI.ToggleTip(
            sec.Body,
            def.SolidColor,
            conf.SolidColor,
            v => { conf.SolidColor = v; Save(); },
            "Solid Color",
            "combo_solidcolor",
            "Use a single flat color instead of blending between Low and High Combo Color as the combo grows."
        );

        GenerateUI.ToggleTip(
            sec.Body,
            def.PerfectColorEnabled,
            conf.PerfectColorEnabled,
            v => { conf.PerfectColorEnabled = v; Save(); },
            "Perfect Color (at Max)",
            "combo_perfectcolor_enabled",
            "Switch to Perfect Color once the combo reaches Color Max Combo, instead of staying on High Combo Color."
        );

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColorLow(),
            conf.GetColorLow(),
            c => { conf.SetColorLow(c); },
            c => { conf.SetColorLow(c); Save(); },
            "Low Combo Color",
            "combo_colorlow"
        );

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColorHigh(),
            conf.GetColorHigh(),
            c => { conf.SetColorHigh(c); },
            c => { conf.SetColorHigh(c); Save(); },
            "High Combo Color",
            "combo_colorhigh"
        );

        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetPerfectColor(),
            conf.GetPerfectColor(),
            c => { conf.SetPerfectColor(c); },
            c => { conf.SetPerfectColor(c); Save(); },
            "Perfect Color",
            "combo_perfectcolor"
        );
    }
}
