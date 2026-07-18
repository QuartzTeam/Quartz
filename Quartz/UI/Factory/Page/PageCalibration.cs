using Quartz.Core;
using Quartz.Features.Calibration;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageCalibration {
    public static void AppendTo(Transform content) {
        Calibration.EnsureConf();
        CalibrationSettings conf = Calibration.Conf;
        CalibrationSettings def = new();
        void Save() => Calibration.Save();
        GenerateUI.CollapsibleSection sec = GenerateUI.FlatSection(content, "Calibration");
        GenerateUI.ToggleTip(
            sec.Body, def.ShowPopupOnDeath, conf.ShowPopupOnDeath,
            v => { conf.ShowPopupOnDeath = v; Save(); },
            "Show Popup on Death", "calibration_popup_on_death",
            "When you die, offer to update your input offset from this run's average timing."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.DetailedDisplay, conf.DetailedDisplay,
            v => { conf.DetailedDisplay = v; Save(); },
            "Detailed Calibration Display", "calibration_detailed_display",
            "Show average/max/min timing on the in-game calibration screen."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.FloatOffsetEnabled, conf.FloatOffsetEnabled,
            v => { conf.FloatOffsetEnabled = v; Save(); },
            "Decimal (Sub-Millisecond) Offset", "calibration_float_offset",
            "Allow the input offset to use decimals instead of whole milliseconds. Ctrl/Shift give .1/.01ms steps in the pause menu."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_CALIBRATION_SONG", "Calibration Song");
        GenerateUI.SnapSlider(sec.Body, "Pitch", "calibration_song_pitch",
            def.SongPitch, 1f, 500f, conf.SongPitch, "0.#' %'", 0.5f,
            v => conf.SongPitch = v, null, Save);
        GenerateUI.SnapSlider(sec.Body, "Repeat Song", "calibration_song_repeat",
            def.SongRepeat, 0f, 20f, conf.SongRepeat, "0 times", 1f,
            v => conf.SongRepeat = Mathf.RoundToInt(v), null, Save);
        GenerateUI.ToggleTip(
            sec.Body, def.SongUseMinimum, conf.SongUseMinimum,
            v => {
                conf.SongUseMinimum = v;
                if(!v) conf.SongMinimum = 0;
                Save();
            },
            "Use Minimum Offset Value", "calibration_song_use_minimum",
            "Fixes calibration values reading smaller than they should once the calibration planet has gone around a full loop."
        );
        if(conf.SongUseMinimum) {
            GenerateUI.SnapSlider(sec.Body, "Minimum Value", "calibration_song_minimum",
                def.SongMinimum, 0f, 2000f, conf.SongMinimum, "0 ms", 10f,
                v => conf.SongMinimum = Mathf.RoundToInt(v), null, Save);
        }
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "SECTION_CALIBRATION_TIMING_HISTORY", "Timing History");
        BuildHistoryList(sec.Body, "CALIBRATION_TIMING_CURRENT_MAP", "Current Map", CalibrationTimingLogger.RecentForCurrentMap());
        BuildHistoryList(sec.Body, "CALIBRATION_TIMING_ALL_MAPS", "All Maps", CalibrationTimingLogger.RecentAll());
    }
    private static void BuildHistoryList(Transform parent, string labelKey, string labelDefault, IReadOnlyList<float> entries) {
        GenerateUI.Localize(GenerateUI.AddMutedText(GenerateUI.Row(parent, 28f), 18f), labelKey, labelDefault);
        if(entries.Count == 0) {
            GenerateUI.Localize(GenerateUI.AddMutedText(GenerateUI.Row(parent, 28f)), "CALIBRATION_TIMING_NONE", "No recorded timings yet.");
            return;
        }
        string applyText = MainCore.Tr.Get("CALIBRATION_TIMING_APPLY", "Apply");
        for(int i = entries.Count - 1; i >= 0; i--) {
            float value = entries[i];
            RectTransform row = GenerateUI.Row(parent, 36f);
            GenerateUI.AddMutedText(row).text = Calibration.FormatMs(value) + "ms";
            GenerateUI.MiniButton(row, applyText, null, -16f, 90f, () => Calibration.SetOffsetMs(value));
        }
    }
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}
