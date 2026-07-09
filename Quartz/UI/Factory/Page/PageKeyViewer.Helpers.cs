using Quartz.Core;
using Quartz.Features.KeyViewer;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Panes;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;

using TMPro;

namespace Quartz.UI.Factory.Page;

// Helpers + nested types extracted from PageKeyViewer.cs.
internal static partial class PageKeyViewer {
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));

    private static string FootStyleName(int s) => s <= 0
        ? MainCore.Tr.Get("KEYVIEWER_FOOT_NONE", "None")
        : string.Format(MainCore.Tr.Get("KEYVIEWER_FOOT_COUNT", "{0} Keys"), s * 2);

    private static string StyleName(int style) => style switch {
        0 => MainCore.Tr.Get("KEYVIEWER_STYLE_10", "10 Keys"),
        1 => MainCore.Tr.Get("KEYVIEWER_STYLE_12", "12 Keys"),
        3 => MainCore.Tr.Get("KEYVIEWER_STYLE_20", "20 Keys"),
        4 => MainCore.Tr.Get("KEYVIEWER_STYLE_8", "8 Keys"),
        5 => MainCore.Tr.Get("KEYVIEWER_STYLE_14", "14 Keys"),
        _ => MainCore.Tr.Get("KEYVIEWER_STYLE_16", "16 Keys"),
    };

    private static string DmOutOfLimiterName(int mode) => mode switch {
        0 => MainCore.Tr.Get("KEYVIEWER_DM_LIMITER_HIDE", "Hide"),
        2 => MainCore.Tr.Get("KEYVIEWER_DM_LIMITER_FULL_PRESS", "Full Press"),
        _ => MainCore.Tr.Get("KEYVIEWER_DM_LIMITER_RAIN_ONLY", "Rain Only"),
    };

    private static UISlider AddSlider(
        Transform body, string label, string id,
        float defVal, float min, float max, float val,
        string format, float step,
        Action<float> setter, Action save
    ) => GenerateUI.SnapSlider(body, label, id, defVal, min, max, val, format, step, setter, null, save);

    private static void AddColor(
        Transform body, string label, string id,
        Color defColor, Color current, Action<Color> setter,
        Action apply, Action save, Action refreshPreview
    ) => GenerateUI.ColorPicker(
            GenerateUI.Row(body),
            defColor,
            current,
            c => { setter(c); apply(); refreshPreview(); },
            c => { setter(c); apply(); refreshPreview(); save(); },
            label,
            id
        );

    // Bridges KeyViewerOverlay.OnKeyPressChanged into a UIObject so PaneHost's
    // dispose-before-destroy step unsubscribes it automatically whenever the
    // live-preview pane's content changes (new selection, tab switch, panel
    // close) — no separate cleanup path to remember.
    private sealed class LiveKeyPreviewHandle : UIObject {
        private readonly Action<KeyViewerOverlay.KeyPressChangedEventArgs> handler;

        public LiveKeyPreviewHandle(RectTransform rect, Action<KeyViewerOverlay.KeyPressChangedEventArgs> handler)
            : base("livekeypreview", rect) {
            this.handler = handler;
            KeyViewerOverlay.OnKeyPressChanged += handler;
        }

        public override void Dispose() {
            base.Dispose();
            KeyViewerOverlay.OnKeyPressChanged -= handler;
        }
    }

    // Polls for the next key press while the preview is armed. Focusing the
    // label input cancels the capture so typing doesn't rebind the key.
    private sealed class KeyCaptureRunner : MonoBehaviour {
        public Func<bool> IsListening;
        public Func<bool> ShouldCancel;
        public Action<KeyCode> OnCaptured;
        public Action OnCancelled;
        public Action OnDestroyed;

        private static readonly KeyCode[] allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        // Previous-frame hook-held state for the keys Unity's legacy Input is
        // blind to (Korean Hangul / Hanja, reported as Right Alt / Right
        // Control). Tracked every frame — even while idle — so arming a listen
        // never reads a stale rising edge from a key already held.
        private bool prevHookRAlt;
        private bool prevHookRCtrl;

        private void Update() {
            bool hookRAlt = Features.KeyLimiter.KeyLimiter.HookKeyHeld(KeyCode.RightAlt);
            bool hookRCtrl = Features.KeyLimiter.KeyLimiter.HookKeyHeld(KeyCode.RightControl);
            bool rAltEdge = hookRAlt && !prevHookRAlt;
            bool rCtrlEdge = hookRCtrl && !prevHookRCtrl;
            prevHookRAlt = hookRAlt;
            prevHookRCtrl = hookRCtrl;

            if(IsListening == null || !IsListening()) return;

            if(Input.GetKeyDown(KeyCode.Escape) || (ShouldCancel?.Invoke() ?? false)) {
                OnCancelled?.Invoke();
                return;
            }

            // Hook fallback first: Hangul/Hanja (Right Alt / Right Control) never
            // reach Unity's Input, so Input.anyKeyDown stays false and the loop
            // below would never see them. The SkyHook-fed held state is the only
            // path that does. A normal keyboard's Right Alt / Right Control still
            // lands on the Unity path below (it fires GetKeyDown), so this only
            // matters for the keys Unity genuinely can't report.
            if(rCtrlEdge) {
                OnCaptured?.Invoke(KeyCode.RightControl);
                return;
            }
            if(rAltEdge) {
                OnCaptured?.Invoke(KeyCode.RightAlt);
                return;
            }

            if(!Input.anyKeyDown) return;

            // Numpad Enter and Return can land on the same frame on some
            // keyboards; the loop below would bind Return first (lower value),
            // so capture the distinct numpad code when it's down.
            if(Input.GetKeyDown(KeyCode.KeypadEnter)) {
                OnCaptured?.Invoke(KeyCode.KeypadEnter);
                return;
            }

            foreach(KeyCode key in allKeys) {
                if(key == KeyCode.None || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)) continue;
                if(Input.GetKeyDown(key)) {
                    OnCaptured?.Invoke(key);
                    return;
                }
            }
        }

        private void OnDestroy() => OnDestroyed?.Invoke();
    }
}
