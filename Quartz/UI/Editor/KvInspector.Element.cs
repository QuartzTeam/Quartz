using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private static readonly string[] StatTypes = ["kps", "kpsAvg", "kpsMax", "total"];
    private static readonly string[] GraphTypes = ["line", "bar"];
    private void BuildElementTab(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        KvElement first = batch[0];
        bool single = batch.Length == 1;
        if(single && first.Kind == KvElementKind.Key) BuildBinding(root, tracked, first);
        else if(AllOf(batch, KvElementKind.Stat)) {
            Header(root, "KVI_SEC_STAT", "Stat");
            StatSegments(root, batch);
        }
        else if(AllGraphs(batch)) BuildGraph(root, tracked, batch);
        Header(root, "KVI_SEC_LABEL", "Label");
        Flag(
            root, tracked, "Show Label", "kvi_label_enabled", true,
            batch, el => el.LabelEnabled, (el, v) => el.LabelEnabled = v, rebuild: true
        ).Rect.AddToolTip(
            "DESC_KVI_LABEL_ENABLED",
            "Draw this element's label. With it off the counter spreads over the whole box, and the box, border and rain are untouched."
        );
        bool anyLabel = false;
        foreach(KvElement el in batch)
            if(el.LabelEnabled) anyLabel = true;
        bool allKeys = AllOf(batch, KvElementKind.Key);
        if(anyLabel) {
            TextField(root, tracked, batch, "kvi_display", "Label (empty = automatic)",
                el => el.DisplayText, (el, v) => el.DisplayText = v);
            if(allKeys) {
                TextField(root, tracked, batch, "kvi_display_pressed", "Label While Pressed (empty = same)",
                    el => el.PressedText, (el, v) => el.PressedText = v);
            }
        }
        if(allKeys) {
            Header(root, "KVI_SEC_COUNT", "Press Count");
            Flag(
                root, tracked, "Count Toward Total", "kvi_countintotal", true,
                batch, el => el.CountInTotal, (el, v) => el.CountInTotal = v
            ).Rect.AddToolTip(
                "DESC_KVI_COUNTINTOTAL",
                "Include this key's presses in the Total stat. Turn it off for foot keys and anything you do not want counted."
            );
            if(single) NumField(root, tracked, "Presses", "kvi_count", first.Count, v => first.Count = Mathf.RoundToInt(v));
            Btn(root, tracked, "Reset Count", "kvi_count_reset", () => {
                Edit(() => {
                    foreach(KvElement el in batch) el.Count = 0;
                });
                Push();
            }).SetDanger();
        }
        Header(root, "KVI_SEC_GEOMETRY", "Position and Size");
        if(single) {
            NumField(root, tracked, "X", "kvi_x", first.X, v => first.X = v);
            NumField(root, tracked, "Y", "kvi_y", first.Y, v => first.Y = v);
            NumField(root, tracked, "Width", "kvi_w", first.W, v => first.W = v);
            NumField(root, tracked, "Height", "kvi_h", first.H, v => first.H = v);
        }
        else {
            Num(root, tracked, "Width", "kvi_w", first.W, KvElement.MinSize, 500f, "0 px", 1f,
                batch, el => el.W, (el, v) => el.W = v);
            Num(root, tracked, "Height", "kvi_h", first.H, KvElement.MinSize, 500f, "0 px", 1f,
                batch, el => el.H, (el, v) => el.H = v);
        }
        BuildHiddenFlag(root, tracked);
        if(!single) BuildArrange(root, tracked, batch);
    }
    private void StatSegments(RectTransform root, KvElement[] batch) => Segments(
        root, StatTypes, StatName, StatKey,
        MatchMulti(StatTypes, batch, el => el.StatType, StatTypes[0]),
        v => {
            Edit(() => {
                foreach(KvElement el in batch) el.StatType = v;
            });
            Push();
        }
    );
    private void BuildBinding(RectTransform root, List<UIObject> tracked, KvElement el) {
        Header(root, "KVI_SEC_BINDING", "Key");
        TextMeshProUGUI bound = GenerateUI.AddMutedText(GenerateUI.Row(root, 30f), 17f, 0.6f);
        bound.text = string.Format(MainCore.Tr.Get("KVI_KEY_CURRENT", "Bound key: {0}"), KeyLabel(el.KeyCodeValue, el.GlobalKey));
        Btn(root, tracked, "Rebind Key", "kvi_rebind", () => {
            listening = true;
            ghostListening = false;
            Push();
        }).Rect.AddToolTip("DESC_KVI_REBIND", "Click, then press the new key for this element. Esc cancels.");
        TextMeshProUGUI ghost = GenerateUI.AddMutedText(GenerateUI.Row(root, 30f), 17f, 0.6f);
        ghost.text = string.Format(MainCore.Tr.Get("KVI_GHOST_CURRENT", "Ghost key: {0}"), KeyLabel(el.GhostKeyCodeValue, el.GhostKey));
        Btn(root, tracked, "Set Ghost Key", "kvi_ghost_set", () => {
            ghostListening = true;
            listening = false;
            Push();
        }).SetNeutral().Rect.AddToolTip(
            "DESC_KVI_GHOST_SET",
            "Click, then press a second key that lights this element up without counting as a press. Esc cancels."
        );
        Btn(root, tracked, "Clear Ghost Key", "kvi_ghost_clear", () => {
            Edit(() => el.GhostKey = "");
            Push();
        }).SetDanger();
    }
    private void BuildGraph(RectTransform root, List<UIObject> tracked, KvElement[] batch) {
        Header(root, "KVI_SEC_GRAPH", "Graph");
        Segments(root, GraphTypes, GraphName, GraphKey,
            MatchMulti(GraphTypes, batch, el => KvProps.Str(el.Raw, "graphType", "line"), "line"),
            v => {
                Edit(() => {
                    foreach(KvElement el in batch) el.Raw["graphType"] = v;
                });
                Push();
            });
        StatSegments(root, batch);
        Colour(
            root, tracked, "Graph Color", "kvi_graph_color",
            KeyViewerOverlay.HexToColor("#86EFAC", 1f),
            batch, el => KvProps.Color(el.Raw, "graphColor", "#86EFAC", 1f),
            (el, c) => KvProps.SetColor(el.Raw, "graphColor", c), true
        );
        Num(root, tracked, "Graph Speed", "kvi_graph_speed", 1000f, 500f, 5000f, "0 ms", 50f,
            batch, el => KvProps.Float(el.Raw, "graphSpeed", 1000f),
            (el, v) => KvProps.SetInt(el.Raw, "graphSpeed", v));
        Flag(root, tracked, "Show Average Line", "kvi_graph_avg", true,
            batch, el => KvProps.Bool(el.Raw, "showAvgLine", true),
            (el, v) => el.Raw["showAvgLine"] = v);
    }
    private void BindKey(KeyCode key) {
        KvElement el = Single();
        if(el != null && el.Kind == KvElementKind.Key) Edit(() => el.BindKey(key));
        listening = false;
        Push();
    }
    private void BindGhost(KeyCode key) {
        KvElement el = Single();
        if(el != null && el.Kind == KvElementKind.Key) Edit(() => el.BindGhostKey(key));
        ghostListening = false;
        Push();
    }
    private KvElement Single() => canvas.Selection.Count == 1 ? canvas.Selection[0] : null;
    private static string KeyLabel(KeyCode key, string globalKey) {
        if(key != KeyCode.None) return KeyViewerOverlay.KeyCodeShortLabel(key);
        return string.IsNullOrEmpty(globalKey) ? MainCore.Tr.Get("KVI_KEY_NONE", "none") : globalKey;
    }
    private static string StatName(string s) => s switch {
        "kpsAvg" => MainCore.Tr.Get("KVI_STAT_KPSAVG", "Avg"),
        "kpsMax" => MainCore.Tr.Get("KVI_STAT_KPSMAX", "Max"),
        "total" => MainCore.Tr.Get("KVI_STAT_TOTAL", "Total"),
        _ => MainCore.Tr.Get("KVI_STAT_KPS", "KPS"),
    };
    private static string StatKey(string s) => s switch {
        "kpsAvg" => "KVI_STAT_KPSAVG",
        "kpsMax" => "KVI_STAT_KPSMAX",
        "total" => "KVI_STAT_TOTAL",
        _ => "KVI_STAT_KPS",
    };
    private static string GraphName(string s) => s == "bar"
        ? MainCore.Tr.Get("KVI_GRAPH_BAR", "Bar")
        : MainCore.Tr.Get("KVI_GRAPH_LINE", "Line");
    private static string GraphKey(string s) => s == "bar" ? "KVI_GRAPH_BAR" : "KVI_GRAPH_LINE";
}
