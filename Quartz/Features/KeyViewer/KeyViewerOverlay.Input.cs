using Quartz.Core;
using Quartz.Features;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static readonly bool macRuntime = KeyLimiter.KeyLimiter.IsMacOSRuntime();
    private static readonly Dictionary<KeyCode, bool> hookFallback = new();
    private static bool KeyHeld(KeyCode key) {
        if(key == KeyCode.None) return false;
        if(macRuntime && KeyLimiter.KeyLimiter.TryMacPhysicalKeyHeld(key, out bool physical))
            return physical;
        try {
            if(Input.GetKey(key)) return true;
            if(!macRuntime) {
                KeyCode twin = NumpadNavTwin(key);
                if(twin != KeyCode.None && Input.GetKey(twin)) return true;
            }
        } catch {
            return false;
        }
        return IsHookFallbackKey(key) && KeyLimiter.KeyLimiter.HookKeyHeld(key);
    }
    private static bool IsHookFallbackKey(KeyCode key) {
        if(!hookFallback.TryGetValue(key, out bool tracked)) {
            tracked = KeyLimiter.KeyLimiter.IsHookTrackedKey(key);
            hookFallback[key] = tracked;
        }
        return tracked;
    }
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
    private static void RebuildKeyMap() {
        foreach(List<Box> list in keyMap.Values) list.Clear();
        pollBoxes.Clear();
        uncoveredBindings = 0;
        drainBuffer.Clear();
        KvInputQueue.Reset();
        foreach(Box box in boxes) {
            box.HookCovered = false;
            box.GhostHookCovered = false;
            box.LitUntil = 0f;
            if(box.IsStat || box.Dm == null || box.Dm.IsStat) continue;
            bool bound = false;
            if(box.Key != KeyCode.None) {
                MapKey(box.Key, box);
                uncoveredBindings++;
                bound = true;
            }
            if(box.Dm.GhostKeyCode != KeyCode.None) {
                MapKey(box.Dm.GhostKeyCode, box);
                uncoveredBindings++;
                bound = true;
            }
            if(bound) pollBoxes.Add(box);
        }
    }
    private static void MapKey(KeyCode key, Box box) {
        if(!keyMap.TryGetValue(key, out List<Box> list)) {
            list = [];
            keyMap[key] = list;
        }
        if(!list.Contains(box)) list.Add(box);
    }
    private static void DrainInputEvents(float frameNow) {
        if(!KvInputQueue.HookActive) return;
        if(KvInputQueue.TakeOverflow()) {
            resyncRequested = true;
            KvInputQueue.RecoverFromGap();
        }
        drainBuffer.Clear();
        KvInputQueue.Drain(drainBuffer);
        for(int i = 0; i < drainBuffer.Count; i++) {
            KvInputQueue.Ev ev = drainBuffer[i];
            if(!keyMap.TryGetValue((KeyCode)ev.Key, out List<Box> list)) continue;
            KeyCode key = (KeyCode)ev.Key;
            for(int b = 0; b < list.Count; b++) {
                Box box = list[b];
                if(box.Key == key) {
                    MarkCovered(box, ghost: false);
                    ApplyPhysicalEdge(box, ev.Down, ev.Time, frameNow);
                }
                if(box.Dm != null && box.Dm.GhostKeyCode == key) {
                    MarkCovered(box, ghost: true);
                    ApplyGhostEdge(box, ev.Down, ev.Time);
                }
            }
        }
        drainBuffer.Clear();
    }
    private static void MarkCovered(Box box, bool ghost) {
        if(ghost) {
            if(box.GhostHookCovered) return;
            box.GhostHookCovered = true;
        } else {
            if(box.HookCovered) return;
            box.HookCovered = true;
        }
        uncoveredBindings--;
    }
    private static void PollUncoveredKeys(float now) {
        bool resync = resyncRequested;
        resyncRequested = false;
        if(!resync && uncoveredBindings <= 0) return;
        for(int i = 0; i < pollBoxes.Count; i++) {
            Box box = pollBoxes[i];
            if((resync || !box.HookCovered) && box.Key != KeyCode.None) {
                bool pressed = KeyHeld(box.Key);
                if(pressed != box.RawPressed) ApplyPhysicalEdge(box, pressed, now, now);
            }
            DmNoteSpec spec = box.Dm;
            if(spec == null || spec.GhostKeyCode == KeyCode.None) continue;
            if(!resync && box.GhostHookCovered) continue;
            bool ghost = KeyHeld(spec.GhostKeyCode);
            if(ghost != box.GhostPressed) ApplyGhostEdge(box, ghost, now);
        }
    }
    private static void ApplyPhysicalEdge(Box box, bool down, float time, float frameNow) {
        if(down == box.RawPressed) return;
        box.RawPressed = down;
        if(!down) {
            EndDmNoteRain(box, time);
            return;
        }
        RecordDmPress(box, time);
        BeginDmNoteRain(box, time);
        if(dmMinLitSeconds > 0f)
            box.LitUntil = Mathf.Max(box.LitUntil, Mathf.Max(time, frameNow) + dmMinLitSeconds);
    }
    private static void ApplyGhostEdge(Box box, bool down, float time) {
        DmNoteSpec spec = box.Dm;
        if(spec == null || down == box.GhostPressed) return;
        box.GhostPressed = down;
        if(down) {
            if(Conf.DmNoteEffect && spec.NoteEnabled && rainManager != null)
                box.LastGhostRain = SpawnDmRain(box, time, true);
            return;
        }
        if(box.LastGhostRain == null) return;
        float minLengthSeconds = dmNoteSpeed > 0f ? dmShortNoteMinLengthPx / dmNoteSpeed : 0f;
        box.LastGhostRain.EndTime = Mathf.Max(time,
            box.LastGhostRain.StartTime + Mathf.Max(0.001f, minLengthSeconds));
        box.LastGhostRain = null;
    }
    private static void SyncHookCoverage() {
        bool active = KvInputQueue.HookActive;
        if(active == hookWasActive) return;
        hookWasActive = active;
        if(active) return;
        KvInputQueue.Discard();
        foreach(Box box in boxes) {
            box.HookCovered = false;
            box.GhostHookCovered = false;
        }
        uncoveredBindings = 0;
        foreach(Box box in pollBoxes) {
            if(box.Key != KeyCode.None) uncoveredBindings++;
            if(box.Dm is { GhostKeyCode: not KeyCode.None }) uncoveredBindings++;
        }
    }
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
            box.LitUntil = 0f;
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
        KvInputQueue.Discard();
        foreach(Box box in boxes) {
            if(box.IsStat) continue;
            bool wasPressed = box.Pressed;
            bool pressed = KeyHeld(box.Key);
            bool ghostPressed = false;
            box.LitUntil = 0f;
            if(box.Dm != null) {
                ghostPressed = KeyHeld(box.Dm.GhostKeyCode);
                box.RawPressed = pressed;
                box.Pressed = pressed;
                box.DisplayTargetPressed = pressed;
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
    private static void EnsureInputPrimed(float now) {
        if(!inputWasActive) {
            inputWasActive = true;
            inputPrimed = false;
        }
        if(inputPrimed) return;
        PrimeInputState(now);
        inputPrimed = true;
    }
    private static void MarkInputInactive(float now, bool clearTransientStats) {
        KvInputQueue.Discard();
        if(inputWasActive || inputPrimed) ResetInputState(now, clearTransientStats);
        inputWasActive = false;
        inputPrimed = false;
    }
}
