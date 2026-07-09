using Quartz.Core;
using Quartz.Features;
using UnityEngine;

namespace Quartz.Features.KeyViewer;

// Per-frame input polling, focus gating, numpad/NumLock key-name mapping, and
// the input-state machine (active / primed / inactive transitions driven by
// application focus).
public static partial class KeyViewerOverlay {
    // True while the key is held. Unity's legacy Input is NumLock-aware: with
    // NumLock OFF the numpad keys report as their navigation twins (Keypad0 ->
    // Insert, KeypadPeriod -> Delete, Keypad2 -> DownArrow, ...) and it always
    // reports the numpad Enter as Return (it can't tell them apart). So a box
    // bound to a numpad key would never light with NumLock off — accept the nav
    // twin as a fallback too. (Mirror of the KeypadEnter -> Return special case
    // this replaces. The reverse ambiguity — a real Insert/arrow press lighting
    // a numpad box when NumLock is on — is unavoidable with the Input API and
    // rare in play; the hook-based KeyLimiter path stays NumLock-independent.)
    private static bool KeyHeld(KeyCode key) {
        if(key == KeyCode.None) return false;
        // No per-call focus check (a native call per key per frame): every call
        // site is downstream of the Updater's !Application.isFocused gate.

        try {
            if(Input.GetKey(key)) return true;
            KeyCode twin = NumpadNavTwin(key);
            if(twin != KeyCode.None && Input.GetKey(twin)) return true;
        } catch {
            return false;
        }

        // Unity's Input is blind to the Korean Hangul/Hanja keys (which map to
        // RightAlt/RightControl); fall back to the SkyHook-fed held state, the
        // only path that sees them. Keep this scoped to those hook-only keys:
        // normal key state must come from Unity's per-frame Input snapshot, like
        // JipperKeyViewer, or missed hook release edges can turn into phantom
        // holds and delayed count bursts.
        return IsHookFallbackKey(key) && KeyLimiter.KeyLimiter.HookKeyHeld(key);
    }

    private static bool IsHookFallbackKey(KeyCode key)
        => key is KeyCode.RightAlt or KeyCode.RightControl;

    // The navigation key Unity's legacy Input reports for each numpad key while
    // NumLock is off. KeyCode.None for non-numpad keys (no fallback).
    private static KeyCode NumpadNavTwin(KeyCode key) => key switch {
        KeyCode.KeypadEnter => KeyCode.Return,
        KeyCode.Keypad0 => KeyCode.Insert,
        KeyCode.Keypad1 => KeyCode.End,
        KeyCode.Keypad2 => KeyCode.DownArrow,
        KeyCode.Keypad3 => KeyCode.PageDown,
        KeyCode.Keypad4 => KeyCode.LeftArrow,
        KeyCode.Keypad5 => KeyCode.Clear,
        KeyCode.Keypad6 => KeyCode.RightArrow,
        KeyCode.Keypad7 => KeyCode.Home,
        KeyCode.Keypad8 => KeyCode.UpArrow,
        KeyCode.Keypad9 => KeyCode.PageUp,
        KeyCode.KeypadPeriod => KeyCode.Delete,
        _ => KeyCode.None,
    };

    private static void ResetInputState(float now, bool clearTransientStats) {
        foreach(Box box in boxes) {
            bool wasPressed = box.Pressed;
            bool changed = box.Pressed || box.RawPressed || box.GhostPressed || box.DisplayTargetPressed
                || box.DelayedNotePending || box.LastRain != null || box.LastGhostRain != null;

            if(box.LastRain != null) {
                box.LastRain.EndTime = now;
                box.LastRain = null;
            }
            if(box.LastGhostRain != null) {
                box.LastGhostRain.EndTime = now;
                box.LastGhostRain = null;
            }

            box.Pressed = false;
            box.RawPressed = false;
            box.GhostPressed = false;
            box.DisplayTargetPressed = false;
            box.DisplayTargetTime = now;
            box.DelayedNotePending = false;
            box.DelayedReleasedBeforeStart = false;
            box.DelayedDownTime = 0f;
            box.DelayedStartTime = 0f;
            box.DelayedReleaseTime = -1f;

            if(changed) ApplyBoxColors(box);
            if(wasPressed) RaisePressChanged(box);
        }

        if(clearTransientStats) {
            pressLog.Clear();
            kpsMax = 0;
            kpsSum = 0;
            kpsSamples = 0;
            nextKpsSample = 0f;
        }
    }

    private static void PrimeInputState(float now) {
        foreach(Box box in boxes) {
            if(box.IsStat) continue;

            bool wasPressed = box.Pressed;
            bool pressed = KeyHeld(box.Key);
            bool ghostPressed = false;

            if(box.Dm != null) {
                int limiterMode = Mathf.Clamp(Conf.DmOutOfLimiterMode, 0, 2);
                bool blocked = box.Key != KeyCode.None && pressed && KeyLimiter.KeyLimiter.ShouldBlockKey(box.Key);
                bool hidden = blocked && limiterMode == 0;
                bool rainOnly = blocked && limiterMode == 1;
                bool physicalPressed = pressed && !hidden && !rainOnly;
                ghostPressed = (rainOnly || KeyHeld(box.Dm.GhostKeyCode)) && !hidden;

                box.RawPressed = physicalPressed;
                box.Pressed = physicalPressed;
                box.DisplayTargetPressed = physicalPressed;
                box.DisplayTargetTime = now;
                box.GhostPressed = ghostPressed;
                box.DelayedNotePending = false;
                box.DelayedReleasedBeforeStart = false;
                box.DelayedReleaseTime = -1f;
                ApplyBoxColors(box);
                if(wasPressed != box.Pressed) RaisePressChanged(box);
                continue;
            }

            if(Conf.RainEnabled && box.RainGroup != 0 && box.GhostKey != KeyCode.None)
                ghostPressed = KeyHeld(box.GhostKey);

            box.Pressed = pressed;
            box.GhostPressed = ghostPressed;
            ApplyBoxColors(box);
            if(wasPressed != box.Pressed) RaisePressChanged(box);
        }
    }

    private static bool InputReady(float now) {
        if(!inputWasActive) {
            inputWasActive = true;
            inputPrimed = false;
        }

        if(inputPrimed) return true;

        PrimeInputState(now);
        inputPrimed = true;
        return false;
    }

    private static void MarkInputInactive(float now, bool clearTransientStats) {
        if(inputWasActive || inputPrimed) ResetInputState(now, clearTransientStats);
        inputWasActive = false;
        inputPrimed = false;
    }
}