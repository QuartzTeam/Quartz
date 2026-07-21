using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Quartz.Compat.Game;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private enum InspTab {
        Element,
        Style,
        Note,
        Counter,
        Settings,
    }
    private static readonly InspTab[] KeyTabs = [InspTab.Element, InspTab.Style, InspTab.Note, InspTab.Counter, InspTab.Settings];
    private static readonly InspTab[] StatTabs = [InspTab.Element, InspTab.Style, InspTab.Counter, InspTab.Settings];
    private static readonly InspTab[] PlainTabs = [InspTab.Element, InspTab.Style, InspTab.Settings];
    private static readonly InspTab[] EmptyTabs = [InspTab.Element, InspTab.Settings];
    private readonly KvCanvas canvas;
    private InspTab tab = InspTab.Element;
    private bool listening;
    private bool ghostListening;
    private string streaming;
    private KeyCaptureRunner capture;
    private RectTransform host;
    private RectTransform tabsHost;
    private RectTransform settingsHost;
    private Action onSettingsShown;
    private UIScrollController scroll;
    private readonly List<UIObject> tracked = [];
    private KvInspector(KvCanvas canvas) => this.canvas = canvas;
    internal static KvInspector Attach(KvCanvas canvas) {
        KvInspector insp = new(canvas);
        canvas.SelectionChanged += insp.OnSelectionChanged;
        canvas.Changed += insp.SyncToolbar;
        canvas.InputSuppressed = () => insp.listening || insp.ghostListening;
        insp.capture = canvas.Rect.gameObject.AddComponent<KeyCaptureRunner>();
        insp.capture.IsListening = () => insp.listening || insp.ghostListening;
        insp.capture.ShouldCancel = () => {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            TMP_InputField field = sel != null ? sel.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        };
        insp.capture.OnCaptured = key => {
            if(insp.ghostListening) insp.BindGhost(key);
            else insp.BindKey(key);
        };
        insp.capture.OnCancelled = () => {
            insp.listening = false;
            insp.ghostListening = false;
            insp.Push();
        };
        return insp;
    }
    internal void BindSettings(RectTransform hostRect, Action onShown) {
        settingsHost = hostRect;
        onSettingsShown = onShown;
    }
    internal void BindHost(RectTransform tabsRect, RectTransform hostRect, UIScrollController scrollController) {
        tabsHost = tabsRect;
        host = hostRect;
        scroll = scrollController;
        Push();
    }
    internal void Dispose() {
        canvas.SelectionChanged -= OnSelectionChanged;
        canvas.Changed -= SyncToolbar;
        canvas.InputSuppressed = null;
        DisposeTracked();
        host = null;
        tabsHost = null;
        settingsHost = null;
        onSettingsShown = null;
        scroll = null;
    }
    private void OnSelectionChanged() {
        listening = false;
        ghostListening = false;
        SyncToolbar();
        Push();
        if(tab != InspTab.Settings) scroll?.ScrollTo(0f);
    }
    private void DisposeTracked() {
        foreach(UIObject obj in tracked) obj?.Dispose();
        tracked.Clear();
    }
    internal void Push() {
        streaming = null;
        DisposeTracked();
        if(host == null) return;
        GenerateUI.ClearChildren(host);
        InspTab[] tabs = TabsForSelection();
        if(Array.IndexOf(tabs, tab) < 0) tab = InspTab.Element;
        if(tabsHost != null) {
            GenerateUI.ClearChildren(tabsHost);
            KvTabs.Build(tabsHost, tabs, TabName, TabKey, tab, t => {
                tab = t;
                Push();
                scroll?.ScrollTo(0f);
            });
        }
        bool settings = tab == InspTab.Settings && settingsHost != null;
        if(settingsHost != null) settingsHost.gameObject.SetActive(settings);
        host.gameObject.SetActive(!settings);
        if(settings) {
            onSettingsShown?.Invoke();
            return;
        }
        Build(host, tracked);
    }
    private InspTab[] TabsForSelection() {
        IReadOnlyList<KvElement> sel = canvas.Selection;
        if(sel.Count == 0) return EmptyTabs;
        if(sel.Count == 1) return TabsFor(sel[0].Kind);
        bool key = false, stat = false;
        foreach(KvElement el in sel) {
            if(el.Kind == KvElementKind.Key) key = true;
            else if(el.Kind == KvElementKind.Stat) stat = true;
        }
        return key ? KeyTabs : stat ? StatTabs : PlainTabs;
    }
    private void Build(RectTransform root, List<UIObject> tracked) {
        IReadOnlyList<KvElement> sel = canvas.Selection;
        if(sel.Count == 0) {
            GenerateUI.AddLocalizedMutedText(
                GenerateUI.Row(root, 30f), "KVI_EMPTY",
                "Select an element on the canvas to edit it. Drag a box around several to edit them together.",
                17f, 0.45f
            );
            return;
        }
        KvElement[] batch = [.. sel];
        TextMeshProUGUI title = GenerateUI.AddText(GenerateUI.Row(root, 36f));
        title.fontSize = 24f;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.text = batch.Length == 1
            ? TitleFor(batch[0])
            : string.Format(MainCore.Tr.Get("KVI_MULTI_TITLE", "{0} elements selected"), batch.Length);
        switch(tab) {
            case InspTab.Style:
                BuildStyleTab(root, tracked, batch);
                break;
            case InspTab.Note:
                BuildNoteTab(root, tracked, OfKind(batch, KvElementKind.Key));
                break;
            case InspTab.Counter:
                BuildCounterTab(root, tracked, KeyLike(batch));
                break;
            default:
                BuildElementTab(root, tracked, batch);
                break;
        }
    }
    private string TitleFor(KvElement el) {
        if(listening) return MainCore.Tr.Get("KVI_LISTENING", "Press a key... (Esc cancels)");
        if(ghostListening) return MainCore.Tr.Get("KVI_GHOST_LISTENING", "Press the ghost key... (Esc cancels)");
        return string.Format(MainCore.Tr.Get("KVI_TITLE", "Editing {0}"), KindName(el));
    }
    private static string KindName(KvElement el) => el.Kind switch {
        KvElementKind.Key => MainCore.Tr.Get("KVI_KIND_KEY", "key"),
        KvElementKind.Stat => MainCore.Tr.Get("KVI_KIND_STAT", "stat"),
        KvElementKind.Graph => MainCore.Tr.Get("KVI_KIND_GRAPH", "graph"),
        _ => MainCore.Tr.Get("KVI_KIND_KNOB", "knob"),
    };
    private static InspTab[] TabsFor(KvElementKind kind) => kind switch {
        KvElementKind.Key => KeyTabs,
        KvElementKind.Stat => StatTabs,
        _ => PlainTabs,
    };
    private static string TabName(InspTab t) => t switch {
        InspTab.Style => MainCore.Tr.Get("KVI_TAB_STYLE", "Style"),
        InspTab.Note => MainCore.Tr.Get("KVI_TAB_NOTE", "Rain"),
        InspTab.Counter => MainCore.Tr.Get("KVI_TAB_COUNTER", "Counter"),
        InspTab.Settings => MainCore.Tr.Get("KVI_TAB_SETTINGS", "Settings"),
        _ => MainCore.Tr.Get("KVI_TAB_ELEMENT", "Element"),
    };
    private static string TabKey(InspTab t) => t switch {
        InspTab.Style => "KVI_TAB_STYLE",
        InspTab.Note => "KVI_TAB_NOTE",
        InspTab.Counter => "KVI_TAB_COUNTER",
        InspTab.Settings => "KVI_TAB_SETTINGS",
        _ => "KVI_TAB_ELEMENT",
    };
    private void Edit(Action apply) {
        streaming = null;
        canvas.PushHistory();
        apply();
        canvas.Refresh();
        canvas.Mutated();
    }
    private void Stream(string owner, Action apply) {
        if(streaming != owner) {
            streaming = owner;
            canvas.PushHistory();
        }
        apply();
        canvas.Refresh();
    }
    private void Commit(string owner, Action apply) {
        if(streaming != owner) {
            streaming = owner;
            canvas.PushHistory();
        }
        apply();
        streaming = null;
        canvas.Refresh();
        canvas.Mutated();
    }
    private static TextMeshProUGUI Header(RectTransform root, string key, string text) =>
        KvWidgets.Header(root, key, text);
    private UISlider Num(
        RectTransform root, List<UIObject> tracked, string label, string id,
        float def, float min, float max, float value, string format, float step, Action<float> write
    ) {
        float Snap(float v) => Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
        UISlider s = KvWidgets.Slider(
            GenerateUI.Row(root), def, min, max, value, Snap, null, null, label, id
        );
        s.Format = format;
        s.OnChanged = v => Stream(id, () => write(v));
        s.OnComplete = v => Commit(id, () => write(v));
        tracked.Add(s);
        return s;
    }
    private UIToggle Flag(
        RectTransform root, List<UIObject> tracked, string label, string id,
        bool def, bool value, Action<bool> write, bool rebuild = false
    ) {
        UIToggle t = KvWidgets.Toggle(
            GenerateUI.Row(root), def, value,
            v => {
                Edit(() => write(v));
                if(rebuild) Push();
            },
            label, id
        );
        tracked.Add(t);
        return t;
    }
    private UIColorPicker Colour(
        RectTransform root, List<UIObject> tracked, string label, string id,
        Color def, Color value, Action<Color> write, bool showAlpha
    ) {
        UIColorPicker p = KvWidgets.ColorPicker(
            GenerateUI.Row(root), def, value,
            c => Stream(id, () => write(c)),
            c => Commit(id, () => write(c)),
            label, id, showAlpha
        );
        tracked.Add(p);
        return p;
    }
    private const float NumFieldCaptionW = 84f;
    private UIInput NumField(
        RectTransform root, List<UIObject> tracked, string label, string id,
        float value, Action<float> write
    ) {
        RectTransform row = GenerateUI.Row(root);
        UIInput input = KvWidgets.Input(
            row, "", Fmt(value),
            v => {
                if(TryNum(v, out float parsed)) Stream(id, () => write(parsed));
            },
            label, MainCore.Spr.Get(UISprite.Text128), id
        );
        input.InputField.characterLimit = 10;
        input.InputField.onEndEdit.AddListener(v => {
            if(TryNum(v, out float parsed)) Commit(id, () => write(parsed));
            else if(streaming == id) streaming = null;
        });
        input.Rect.offsetMin = new Vector2(NumFieldCaptionW, 0f);
        TextMeshProUGUI caption = GenerateUI.AddText(row);
        caption.fontSize = 19f;
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        TextCompat.NoWrap(caption);
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.raycastTarget = false;
        GenerateUI.LocalizeById(caption, id, label);
        RectTransform capRect = caption.rectTransform;
        capRect.anchorMin = new Vector2(0f, 0f);
        capRect.anchorMax = new Vector2(0f, 1f);
        capRect.pivot = new Vector2(0f, 0.5f);
        capRect.offsetMin = new Vector2(12f, 0f);
        capRect.offsetMax = Vector2.zero;
        capRect.sizeDelta = new Vector2(NumFieldCaptionW - 12f, 0f);
        tracked.Add(input);
        return input;
    }
    private UIInput TextField(
        RectTransform root, List<UIObject> tracked, KvElement[] batch, string id, string placeholder,
        Func<KvElement, string> read, Action<KvElement, string> write
    ) {
        bool mixed = Mixed(batch, read);
        bool typed = false;
        UIInput input = KvWidgets.Input(
            GenerateUI.Row(root), "", mixed ? "" : read(batch[0]),
            v => {
                typed = true;
                Stream(id, () => {
                    foreach(KvElement el in batch) write(el, v ?? "");
                });
            },
            placeholder, MainCore.Spr.Get(UISprite.Text128), id
        );
        input.InputField.characterLimit = 24;
        input.InputField.onEndEdit.AddListener(v => {
            if(mixed && !typed) return;
            Commit(id, () => {
                foreach(KvElement el in batch) write(el, v ?? "");
            });
        });
        tracked.Add(input);
        return input;
    }
    private static bool TryNum(string s, out float value) => float.TryParse(
        s, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out value
    );
    private static string Fmt(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private UIButton Btn(RectTransform root, List<UIObject> tracked, string label, string id, Action click) {
        UIButton b = KvWidgets.Button(GenerateUI.Row(root, 44f), click, label, id);
        tracked.Add(b);
        return b;
    }
    private static void Segments<T>(
        RectTransform root, IReadOnlyList<T> values, Func<T, string> name, Func<T, string> key,
        T value, Action<T> onChanged
    ) => KvWidgets.Segments(root, values, name, key, value, onChanged);
}
