using UnityEngine;
using UnityEngine.UI;

namespace Quartz.Features.KeyViewer;

// Press-event argument + per-box colour application. The colour write is the
// only "side effect per frame" outside the input poll, so it lives here next
// to the event the live-preview pane subscribes to.
public static partial class KeyViewerOverlay {
    // Raised whenever a box's Pressed state actually flips — never on a
    // per-frame poll of its own, just piggybacked on the edge-detection this
    // overlay's Update loop already does at each of its press/release sites
    // (simple mode, DM Note delayed display, and the prime/reset paths that
    // run when input focus is gained/lost). A settings-window live-preview
    // pane subscribes to mirror a specific slot's box without duplicating any
    // of this polling itself.
    public readonly struct KeyPressChangedEventArgs {
        public readonly int Slot;
        public readonly KeyCode Key;
        public readonly bool Pressed;
        public KeyPressChangedEventArgs(int slot, KeyCode key, bool pressed) {
            Slot = slot;
            Key = key;
            Pressed = pressed;
        }
    }

    public static event Action<KeyPressChangedEventArgs> OnKeyPressChanged;

    private static void RaisePressChanged(Box box) =>
        OnKeyPressChanged?.Invoke(new KeyPressChangedEventArgs(box.Slot, box.Key, box.Pressed));

    private static void ApplyBoxColors(Box box) {
        if(box.Dm != null) {
            bool dmPressed = box.Pressed;
            DmNoteSpec spec = box.Dm;
            box.Border.color = dmPressed ? spec.ActiveOutline : spec.Outline;
            // Solid base for the state; an animated gradient overwrites these per
            // frame in CssTick (the gradient's first stop already seeds them).
            box.Fill.color = dmPressed ? spec.ActiveBg : spec.Bg;
            if(box.Label != null) box.Label.color = dmPressed ? spec.ActiveText : spec.Text;
            if(box.Value != null) box.Value.color = dmPressed ? spec.ActiveCounterText : spec.CounterText;
            // The colour writes above dirty the TMP meshes, whose rebuild wipes
            // per-glyph gradient colours — null the trackers so CssTick re-applies
            // them once (static gradients no longer re-upload every frame).
            box.GradLabelText = null;
            box.GradValueText = null;

            if(spec.NeedsCssState) ApplyCssState(box, dmPressed);
            return;
        }

        bool pressed = box.Pressed;
        int slot = box.Slot;
        box.Border.color = pressed
            ? Conf.PerKeyOr(Conf.PerKeyOutlinePressed, slot, Conf.GetOutlinePressed())
            : Conf.PerKeyOr(Conf.PerKeyOutline, slot, Conf.GetOutline());
        box.Fill.color = pressed
            ? Conf.PerKeyOr(Conf.PerKeyBgPressed, slot, Conf.GetBgPressed())
            : Conf.PerKeyOr(Conf.PerKeyBg, slot, Conf.GetBg());

        Color text = pressed
            ? Conf.PerKeyOr(Conf.PerKeyTextPressed, slot, Conf.GetTextPressed())
            : Conf.PerKeyOr(Conf.PerKeyText, slot, Conf.GetText());
        if(box.Label != null) box.Label.color = text;
        if(box.Value != null) box.Value.color = text;
    }
}