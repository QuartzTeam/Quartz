using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PagePanels {
    private static void BuildStatColorSettings(
        Transform parent, StatEntry entry, Action save, Action rebuild, string idp
    ) {
        StatColor color = entry.EnsureColor();
        bool hasRatio = StatColor.HasRatio(entry.Id);
        if(entry.Id == "fps") {
            GenerateUI.SnapSlider(
                parent, "FPS Update Interval", "overlay_fps_smooth",
                0f, 0f, 2f, MainCore.Conf.FpsRefreshInterval, "0.00 s", 0.05f,
                v => MainCore.Conf.FpsRefreshInterval = v,
                null,
                () => MainCore.ConfMgr.RequestSave()
            ).Rect.AddToolTip(
                "DESC_OVERLAY_FPS_SMOOTH",
                "How long the FPS counter holds each value before updating. "
                + "0 = every frame; higher makes the number tick more slowly and steadily.");
        }
        GenerateUI.Toggle(
            GenerateUI.Row(parent),
            false,
            color.Enabled,
            v => { color.Enabled = v; save(); },
            "Custom Color",
            idp + "_statcolor_on"
        ).Rect.AddToolTip(
            "DESC_PANEL_STATCOLOR_ON",
            "Tints this stat's value by blending the colors below across the stat's own 0–100% range."
        );
        if(StatColor.IsBpm(entry.Id)) {
            UISlider maxBpm = GenerateUI.Slider(
                GenerateUI.Row(parent),
                8000f, 1f, 9999f, color.MaxBpm,
                v => Mathf.Clamp(Mathf.Round(v), 1f, 9999f), null, null,
                "Color Max BPM", idp + "_statcolor_maxbpm"
            );
            maxBpm.Format = "0";
            maxBpm.OnChanged = v => color.MaxBpm = v;
            maxBpm.OnComplete = v => { color.MaxBpm = v; save(); };
            maxBpm.Rect.AddToolTip(
                "DESC_PANEL_STATCOLOR_MAXBPM",
                "BPM that maps to the 100% end of the gradient."
            );
        }
        for(int i = 0; i < color.Points.Count; i++) {
            ColorPoint point = color.Points[i];
            if(hasRatio) {
                RectTransform posRow = GenerateUI.Row(parent);
                UISlider pos = GenerateUI.Slider(
                    posRow,
                    100f, 0f, 100f, point.Pos * 100f,
                    v => Mathf.Clamp(Mathf.Round(v * 2f) * 0.5f, 0f, 100f), null, null,
                    "Position", idp + "_statcolor_pos"
                );
                pos.Format = "0.#' %'";
                pos.OnChanged = v => point.Pos = v * 0.01f;
                pos.OnComplete = v => {
                    point.Pos = v * 0.01f;
                    color.SortPoints();
                    save();
                };
                if(color.Points.Count > 1) {
                    GenerateUI.MiniButton(posRow, "X", "DELETE_SHORT", -8f, 44f, () => {
                        color.Points.Remove(point);
                        save();
                        rebuild();
                    });
                }
            }
            GenerateUI.ColorPicker(
                GenerateUI.Row(parent),
                Color.white,
                point.GetColor(),
                c => point.SetColor(c),
                c => { point.SetColor(c); save(); },
                "Color",
                idp + "_statcolor_color"
            );
            if(!hasRatio && color.Points.Count > 1) {
                GenerateUI.MiniButton(GenerateUI.Row(parent, 40f), "X", "DELETE_SHORT", -8f, 44f, () => {
                    color.Points.Remove(point);
                    save();
                    rebuild();
                });
            }
        }
        if(hasRatio && color.Points.Count < 8) {
            GenerateUI.Button(
                GenerateUI.Row(parent),
                () => {
                    float pos = color.Points.Count > 0 ? 0.5f : 1f;
                    color.Points.Add(new ColorPoint(pos, color.Evaluate(pos)));
                    color.SortPoints();
                    save();
                    rebuild();
                },
                "+ Add Color",
                idp + "_statcolor_add"
            ).SetSecondary();
        }
        if(hasRatio) {
            GenerateUI.Toggle(
                GenerateUI.Row(parent),
                false,
                color.UsePerfect,
                v => { color.UsePerfect = v; save(); },
                "Perfect Color (100%)",
                idp + "_statcolor_perfect"
            ).Rect.AddToolTip(
                "DESC_PANEL_STATCOLOR_PERFECT",
                "Overrides the gradient with this color while the stat sits at exactly 100% — v1's gold accuracy."
            );
            GenerateUI.ColorPicker(
                GenerateUI.Row(parent),
                new Color(1f, 0.854902f, 0f, 1f),
                color.Perfect.GetColor(),
                c => color.Perfect.SetColor(c),
                c => { color.Perfect.SetColor(c); save(); },
                "Perfect Color",
                idp + "_statcolor_perfectcolor"
            );
        }
    }
}
