using UnityEngine;
using UnityEngine.UI;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
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
            box.Fill.color = dmPressed ? spec.ActiveBg : spec.Bg;
            if(box.Label != null && spec.PressedDisplayText.Length > 0 && !spec.InlineStatCounter) {
                box.Label.text = dmPressed ? spec.PressedDisplayText : spec.DisplayText;
                box.GradLabelText = null;
            }
            if(box.Value != null && !spec.CounterShowWhilePressed && box.Value.gameObject.activeSelf == dmPressed) {
                box.Value.gameObject.SetActive(!dmPressed);
                if(box.Label != null) LayoutDmText(box.Label.rectTransform, spec, false, counterHidden: dmPressed);
            }
            if(box.Label != null && (dmPressed ? spec.ActiveLabelGradient : spec.LabelGradient) == null) {
                box.Label.color = dmPressed ? spec.ActiveText : spec.Text;
                box.GradLabelText = null;
            }
            if(box.Value != null && (dmPressed ? spec.ActiveCounterGradient : spec.CounterGradient) == null) {
                box.Value.color = dmPressed ? spec.ActiveCounterText : spec.CounterText;
                box.GradValueText = null;
            }
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
