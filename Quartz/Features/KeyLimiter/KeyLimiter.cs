using Quartz.Core;
using Quartz.IO;
using MonsterLove.StateMachine;
using SkyHook;
using System.Threading;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.KeyLimiter;
internal static partial class KeyLimiter {
    public static SettingsFile<KeyLimiterSettings> ConfMgr { get; private set; }
    public static KeyLimiterSettings Conf => ConfMgr?.Data;
    public static event Action Changed;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = SettingsFile<KeyLimiterSettings>.Loaded("KeyLimiter.json");
        EnsureTicker();
    }
    public static void Save() => ConfMgr?.RequestSave();
    public static bool IsEnabled() {
        EnsureConf();
        return MainCore.IsModEnabled && Conf.Enabled;
    }
    public static bool IsActive() => IsEnabled() && !IsCapturing;
    public static bool IsMenuBlockEnabled() => MainCore.IsModEnabled && MainCore.Conf.BlockInputsWhileMenuOpen;
    public static bool IsMenuBlockActive() => IsMenuBlockEnabled() && Quartz.UI.UICore.IsOpen && !Autoplaying;
    private static bool Autoplaying {
        get { try { return RDC.auto; } catch { return false; } }
    }
    private static int cachedPlayerControlFrame = -1;
    private static bool cachedPlayerControl;
    private static int cachedPlayerControlForHooks;
    public static bool InPlayerControl() {
        int frame = Time.frameCount;
        if(cachedPlayerControlFrame == frame) return cachedPlayerControl;
        cachedPlayerControlFrame = frame;
        SetCachedPlayerControl(false);
        try {
            scrController controller = scrController.instance;
            if(controller == null) return false;
            if(controller.paused || !controller.gameworld) return false;
            SetCachedPlayerControl(((StateBehaviour)controller).stateMachine.GetState() is States state
                && state == States.PlayerControl);
            return cachedPlayerControl;
        } catch {
            SetCachedPlayerControl(false);
            return false;
        }
    }
    public static bool InPlayerControlCached() => Volatile.Read(ref cachedPlayerControlForHooks) != 0;
    private static void SetCachedPlayerControl(bool value) {
        cachedPlayerControl = value;
        Volatile.Write(ref cachedPlayerControlForHooks, value ? 1 : 0);
    }
    private static readonly HashSet<int> cachedAllowedKeys = [];
    private static int[] cachedAllowedSource;
    private static int cachedAllowedLength = -1;
    public static bool IsAllowedKey(KeyCode key) {
        int[] allowed = Conf?.AllowedKeys;
        if(allowed == null) return false;
        if(!ReferenceEquals(allowed, cachedAllowedSource) || allowed.Length != cachedAllowedLength) {
            cachedAllowedKeys.Clear();
            for(int i = 0; i < allowed.Length; i++) {
                cachedAllowedKeys.Add((int)NormalizeKey((KeyCode)allowed[i]));
            }
            cachedAllowedSource = allowed;
            cachedAllowedLength = allowed.Length;
        }
        return cachedAllowedKeys.Contains((int)NormalizeKey(key));
    }
    public static bool IsMouseKey(KeyCode key) => key is >= KeyCode.Mouse0 and <= KeyCode.Mouse6;
    private static KeyCode NavTwinToNumpad(KeyCode key) => key switch {
        KeyCode.Insert => KeyCode.Keypad0,
        KeyCode.End => KeyCode.Keypad1,
        KeyCode.DownArrow => KeyCode.Keypad2,
        KeyCode.PageDown => KeyCode.Keypad3,
        KeyCode.LeftArrow => KeyCode.Keypad4,
        KeyCode.Clear => KeyCode.Keypad5,
        KeyCode.RightArrow => KeyCode.Keypad6,
        KeyCode.Home => KeyCode.Keypad7,
        KeyCode.UpArrow => KeyCode.Keypad8,
        KeyCode.PageUp => KeyCode.Keypad9,
        KeyCode.Delete => KeyCode.KeypadPeriod,
        _ => KeyCode.None,
    };
    public static bool ShouldBlockKey(KeyCode key) {
        if(!IsActive() || !InPlayerControl() || IsMouseKey(key)) return false;
        if(IsAllowedKey(key)) return false;
        KeyCode numpadOrigin = IsMacOSRuntime() ? KeyCode.None : NavTwinToNumpad(key);
        return numpadOrigin == KeyCode.None || !IsAllowedKey(numpadOrigin);
    }
    public static void ToggleAllowedKey(KeyCode key) {
        EnsureConf();
        key = NormalizeKey(key);
        if(key == KeyCode.None || IsMouseKey(key)) return;
        List<int> keys = [.. Conf.AllowedKeys];
        if(!keys.Remove((int)key)) keys.Add((int)key);
        Conf.AllowedKeys = [.. keys];
        PersistChange();
    }
    public static void SetAllowedKeys(int[] keys) {
        EnsureConf();
        Conf.AllowedKeys = keys ?? [];
        PersistChange();
    }
    public static IReadOnlyList<KeyLimiterProfile> Profiles {
        get { EnsureConf(); return Conf.Profiles; }
    }
    public static int ActiveProfileIndex {
        get { EnsureConf(); return Conf.ActiveProfile; }
    }
    public static void SwitchProfile(int index) {
        EnsureConf();
        if(index < 0 || index >= Conf.Profiles.Count || index == Conf.ActiveProfile) return;
        CancelCapture();
        Conf.ActiveProfile = index;
        PersistChange();
    }
    public static void AddProfile() {
        EnsureConf();
        Conf.Profiles.Add(new KeyLimiterProfile {
            Name = "Profile " + (Conf.Profiles.Count + 1),
            Keys = [],
        });
        Conf.ActiveProfile = Conf.Profiles.Count - 1;
        PersistChange();
    }
    public static void RemoveActiveProfile() {
        EnsureConf();
        if(Conf.Profiles.Count <= 1) return;
        CancelCapture();
        Conf.Profiles.RemoveAt(Conf.ActiveProfile);
        if(Conf.ActiveProfile >= Conf.Profiles.Count) Conf.ActiveProfile = Conf.Profiles.Count - 1;
        PersistChange();
    }
    public static void RenameActiveProfile(string name) {
        EnsureConf();
        Conf.ActiveProfileOrDefault().Name = name ?? "";
        PersistChange();
    }
    private const int LegacyAsyncKeyOffset = 0x1000;
    private const int LegacyAsyncKeyMax = LegacyAsyncKeyOffset + 0xFF;
    public static KeyCode NormalizeKey(KeyCode key) {
        key = NormalizeLegacyAsyncKey(key);
        if(key == KeyCode.AltGr) return KeyCode.RightAlt;
        if(key == KeyCode.KeypadEnter) return KeyCode.Return;
        return key;
    }
    public static KeyCode NormalizeNumericKey(int numeric) {
        if(numeric >= 0 && numeric <= 0xFF) {
            KeyCode vk = WindowsVirtualKeyToUnityKey((ushort)numeric);
            if(vk != KeyCode.None) return vk;
        }
        return NormalizeKey((KeyCode)numeric);
    }
    private static KeyCode NormalizeLegacyAsyncKey(KeyCode key) {
        int raw = (int)key;
        if(raw < LegacyAsyncKeyOffset || raw > LegacyAsyncKeyMax) return key;
        KeyCode mapped = WindowsVirtualKeyToUnityKey((ushort)(raw - LegacyAsyncKeyOffset));
        return mapped == KeyCode.None ? key : mapped;
    }
    public static bool IsMouseLabel(KeyLabel label) => label is
        KeyLabel.MouseLeft or KeyLabel.MouseRight or KeyLabel.MouseMiddle or KeyLabel.MouseX1 or KeyLabel.MouseX2;
    public static bool ShouldBlockAsyncKeyFromHook(ushort key, KeyLabel label) {
        if(!IsActive() || !InPlayerControlCached() || IsMouseLabel(label)) return false;
        KeyCode unityKey = HookKeyToPhysicalUnityKey(key, label);
        if(IsMouseKey(unityKey)) return false;
        if(unityKey != KeyCode.None && IsAllowedKey(unityKey)) return false;
        KeyCode mappedKey = GameApi.HookKeyToUnityKey(label);
        if(mappedKey == KeyCode.None && IsAllowedGenericModifierVirtualKey(key)) return false;
        return mappedKey == KeyCode.None || !IsAllowedKey(mappedKey);
    }
    private static bool IsAllowedGenericModifierVirtualKey(ushort key) {
        switch(key) {
            case 0x10:
                return IsAllowedKey(KeyCode.LeftShift) || IsAllowedKey(KeyCode.RightShift);
            case 0x11:
                return IsAllowedKey(KeyCode.LeftControl) || IsAllowedKey(KeyCode.RightControl);
            case 0x12:
                return IsAllowedKey(KeyCode.LeftAlt) || IsAllowedKey(KeyCode.RightAlt)
                    || IsAllowedKey(KeyCode.AltGr);
            default:
                return false;
        }
    }
    private static readonly HashSet<KeyCode> hookHeldKeys = new();
    private static readonly HashSet<KeyCode> hookSeenKeys = new();
    private static volatile bool hookActive;
    private static bool IsHookOnlyKey(KeyCode key) {
        if(key is KeyCode.RightAlt or KeyCode.RightControl) return true;
        return !IsWindowsRuntime() && key is
            KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.LeftAlt;
    }
    private static bool IsNumpadHookKey(KeyCode key) => key is
        KeyCode.Keypad0 or KeyCode.Keypad1 or KeyCode.Keypad2 or KeyCode.Keypad3 or KeyCode.Keypad4 or
        KeyCode.Keypad5 or KeyCode.Keypad6 or KeyCode.Keypad7 or KeyCode.Keypad8 or KeyCode.Keypad9 or
        KeyCode.KeypadPeriod or KeyCode.KeypadDivide or KeyCode.KeypadMultiply or
        KeyCode.KeypadMinus or KeyCode.KeypadPlus;
    public static bool IsHookTrackedKey(KeyCode key) =>
        IsHookOnlyKey(key) || (IsMacOSRuntime() && IsNumpadHookKey(key));
    public static void NoteHookEvent(KeyCode key, bool pressed) {
        if(key == KeyCode.None) return;
        lock(hookSeenKeys) {
            hookSeenKeys.Add(key);
        }
        if(!IsHookTrackedKey(key)) return;
        lock(hookHeldKeys) {
            if(pressed) hookHeldKeys.Add(key);
            else hookHeldKeys.Remove(key);
            hookActive = hookHeldKeys.Count > 0;
        }
    }
    public static bool HookEverSaw(KeyCode key) {
        lock(hookSeenKeys) {
            return hookSeenKeys.Contains(key);
        }
    }
    public static bool HookKeyHeld(KeyCode key) {
        if(!hookActive || key == KeyCode.None) return false;
        lock(hookHeldKeys) {
            return hookHeldKeys.Contains(key);
        }
    }
    public static KeyCode HookKeyToPhysicalUnityKey(ushort key, KeyLabel label) {
        KeyCode labelKey = GameApi.HookKeyToUnityKey(label);
        if(IsNumpadOrArrowKey(labelKey)) return labelKey;
        if(IsWindowsRuntime()) {
            KeyCode hookKey = WindowsVirtualKeyToUnityKey(key);
            if(hookKey != KeyCode.None) return hookKey;
        }
        KeyCode mapped = AsyncLabelToPhysicalUnityKey(label);
        if(mapped != KeyCode.None) return mapped;
        return KeyCode.None;
    }
    private static bool IsNumpadOrArrowKey(KeyCode key) => key is
        KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.LeftArrow or KeyCode.RightArrow or
        KeyCode.Keypad0 or KeyCode.Keypad1 or KeyCode.Keypad2 or KeyCode.Keypad3 or KeyCode.Keypad4 or
        KeyCode.Keypad5 or KeyCode.Keypad6 or KeyCode.Keypad7 or KeyCode.Keypad8 or KeyCode.Keypad9 or
        KeyCode.KeypadPeriod or KeyCode.KeypadDivide or KeyCode.KeypadMultiply or KeyCode.KeypadMinus or
        KeyCode.KeypadPlus or KeyCode.KeypadEnter;
    private static bool IsWindowsRuntime() {
        RuntimePlatform platform = Application.platform;
        return platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor;
    }
    public static bool IsMacOSRuntime() {
        RuntimePlatform platform = Application.platform;
        return platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.OSXEditor;
    }
    [System.Runtime.InteropServices.DllImport(
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool CGEventSourceKeyState(int sourceStateID, ushort keyCode);
    private const int KCGEventSourceStateHidSystemState = 1;
    private static readonly bool MacRuntimeCached = IsMacOSRuntime();
    public static bool TryMacPhysicalKeyHeld(KeyCode key, out bool held) {
        held = false;
        if(!MacRuntimeCached) return false;
        ushort vk = key switch {
            KeyCode.Tab => 0x30,
            _ => ushort.MaxValue,
        };
        if(vk == ushort.MaxValue) return false;
        try {
            held = CGEventSourceKeyState(KCGEventSourceStateHidSystemState, vk);
            return true;
        } catch {
            return false;
        }
    }
    private static readonly Dictionary<KeyLabel, KeyCode> asyncLabelCache = new();
    private static KeyCode AsyncLabelToPhysicalUnityKey(KeyLabel label) {
        lock(asyncLabelCache) {
            if(asyncLabelCache.TryGetValue(label, out KeyCode cached)) return cached;
            KeyCode resolved = ResolveAsyncLabelToPhysicalUnityKey(label);
            asyncLabelCache[label] = resolved;
            return resolved;
        }
    }
    private static KeyCode ResolveAsyncLabelToPhysicalUnityKey(KeyLabel label) {
        string name = label.ToString();
        if(name.Length == 1 && name[0] >= 'A' && name[0] <= 'Z')
            return (KeyCode)((int)KeyCode.A + (name[0] - 'A'));
        if(name.Length == 6 && name.StartsWith("Alpha") && name[5] >= '0' && name[5] <= '9')
            return (KeyCode)((int)KeyCode.Alpha0 + (name[5] - '0'));
        if(name.Length >= 2 && name[0] == 'F'
            && int.TryParse(name[1..], out int functionKey) && functionKey >= 1 && functionKey <= 15)
            return (KeyCode)((int)KeyCode.F1 + (functionKey - 1));
        if(name.Length == 7 && name.StartsWith("Keypad") && name[6] >= '0' && name[6] <= '9')
            return (KeyCode)((int)KeyCode.Keypad0 + (name[6] - '0'));
        return name switch {
            "Escape" => KeyCode.Escape,
            "Grave" => KeyCode.BackQuote,
            "Minus" => KeyCode.Minus,
            "Equal" => KeyCode.Equals,
            "Backspace" => KeyCode.Backspace,
            "Tab" => KeyCode.Tab,
            "LeftBrace" => KeyCode.LeftBracket,
            "RightBrace" => KeyCode.RightBracket,
            "BackSlash" => KeyCode.Backslash,
            "CapsLock" => KeyCode.CapsLock,
            "Semicolon" => KeyCode.Semicolon,
            "Apostrophe" => KeyCode.Quote,
            "Enter" => KeyCode.Return,
            "LShift" or "LeftShift" => KeyCode.LeftShift,
            "RShift" or "RightShift" => KeyCode.RightShift,
            "Comma" => KeyCode.Comma,
            "Dot" => KeyCode.Period,
            "Slash" => KeyCode.Slash,
            "LControl" or "LCtrl" or "LeftControl" or "LeftCtrl" => KeyCode.LeftControl,
            "RControl" or "RCtrl" or "RightControl" or "RightCtrl" or "Hanja" => KeyCode.RightControl,
            "Super" => KeyCode.LeftCommand,
            "LWin" or "LeftWin" or "LeftWindows" => KeyCode.LeftWindows,
            "RWin" or "RightWin" or "RightWindows" => KeyCode.RightWindows,
            "LAlt" => KeyCode.LeftAlt,
            "RAlt" or "AltGr" or "Hangul" => KeyCode.RightAlt,
            "Space" => KeyCode.Space,
            "PrintScreen" => KeyCode.Print,
            "ScrollLock" => KeyCode.ScrollLock,
            "PauseBreak" => KeyCode.Pause,
            "Insert" => KeyCode.Insert,
            "Home" => KeyCode.Home,
            "PageUp" => KeyCode.PageUp,
            "Delete" => KeyCode.Delete,
            "End" => KeyCode.End,
            "PageDown" => KeyCode.PageDown,
            "ArrowUp" => KeyCode.UpArrow,
            "ArrowLeft" => KeyCode.LeftArrow,
            "ArrowDown" => KeyCode.DownArrow,
            "ArrowRight" => KeyCode.RightArrow,
            "NumLock" => KeyCode.Numlock,
            "KeypadSlash" => KeyCode.KeypadDivide,
            "KeypadAsterisk" => KeyCode.KeypadMultiply,
            "KeypadMinus" => KeyCode.KeypadMinus,
            "KeypadDot" => KeyCode.KeypadPeriod,
            "KeypadPlus" => KeyCode.KeypadPlus,
            "KeypadEnter" => KeyCode.KeypadEnter,
            "Application" or "Apps" or "Menu" => KeyCode.Menu,
            "MouseLeft" => KeyCode.Mouse0,
            "MouseRight" => KeyCode.Mouse1,
            "MouseMiddle" => KeyCode.Mouse2,
            "MouseX1" => KeyCode.Mouse3,
            "MouseX2" => KeyCode.Mouse4,
            _ => GameApi.HookKeyToUnityKey(label),
        };
    }
    private static KeyCode WindowsVirtualKeyToUnityKey(ushort key) => key switch {
        0x15 or 0xA5 => KeyCode.RightAlt,
        0x19 or 0xA3 => KeyCode.RightControl,
        0x10 or 0xA0 => KeyCode.LeftShift,
        0x11 or 0xA2 => KeyCode.LeftControl,
        0x12 or 0xA4 => KeyCode.LeftAlt,
        >= 0x30 and <= 0x39 => (KeyCode)((int)KeyCode.Alpha0 + (key - 0x30)),
        >= 0x41 and <= 0x5A => (KeyCode)((int)KeyCode.A + (key - 0x41)),
        >= 0x60 and <= 0x69 => (KeyCode)((int)KeyCode.Keypad0 + (key - 0x60)),
        >= 0x70 and <= 0x7E => (KeyCode)((int)KeyCode.F1 + (key - 0x70)),
        0x5D => KeyCode.Menu,
        0x08 => KeyCode.Backspace,
        0x09 => KeyCode.Tab,
        0x0D => KeyCode.Return,
        0x13 => KeyCode.Pause,
        0x14 => KeyCode.CapsLock,
        0x1B => KeyCode.Escape,
        0x20 => KeyCode.Space,
        0x21 => KeyCode.PageUp,
        0x22 => KeyCode.PageDown,
        0x23 => KeyCode.End,
        0x24 => KeyCode.Home,
        0x25 => KeyCode.LeftArrow,
        0x26 => KeyCode.UpArrow,
        0x27 => KeyCode.RightArrow,
        0x28 => KeyCode.DownArrow,
        0x2C => KeyCode.Print,
        0x2D => KeyCode.Insert,
        0x2E => KeyCode.Delete,
        0x5B => KeyCode.LeftWindows,
        0x5C => KeyCode.RightWindows,
        0x6A => KeyCode.KeypadMultiply,
        0x6B => KeyCode.KeypadPlus,
        0x6D => KeyCode.KeypadMinus,
        0x6E => KeyCode.KeypadPeriod,
        0x6F => KeyCode.KeypadDivide,
        0x90 => KeyCode.Numlock,
        0x91 => KeyCode.ScrollLock,
        0xA1 => KeyCode.RightShift,
        0xBA => KeyCode.Semicolon,
        0xBB => KeyCode.Equals,
        0xBC => KeyCode.Comma,
        0xBD => KeyCode.Minus,
        0xBE => KeyCode.Period,
        0xBF => KeyCode.Slash,
        0xC0 => KeyCode.BackQuote,
        0xDB => KeyCode.LeftBracket,
        0xDC => KeyCode.Backslash,
        0xDD => KeyCode.RightBracket,
        0xDE => KeyCode.Quote,
        _ => KeyCode.None,
    };
    public static bool IsCapturing { get; private set; }
    private static Action<KeyCode> captureOnKey;
    private static Action captureOnEnded;
    public static void StartCapture(Action<KeyCode> onKey, Action onEnded) {
        CancelCapture();
        IsCapturing = true;
        captureOnKey = onKey;
        captureOnEnded = onEnded;
        Keybind.Capturing = true;
        Changed?.Invoke();
    }
    public static void CancelCapture() => EndCapture(KeyCode.None);
    private static void EndCapture(KeyCode key) {
        if(!IsCapturing) return;
        IsCapturing = false;
        Keybind.Capturing = false;
        Action<KeyCode> onKey = captureOnKey;
        Action onEnded = captureOnEnded;
        captureOnKey = null;
        captureOnEnded = null;
        if(key != KeyCode.None && key != KeyCode.Escape) onKey?.Invoke(key);
        onEnded?.Invoke();
        Changed?.Invoke();
    }
    public static void ClearAllowedKeys() {
        EnsureConf();
        Conf.AllowedKeys = [];
        PersistChange();
    }
    public static void ReplaceAllowedKey(KeyCode oldKey, KeyCode newKey) {
        EnsureConf();
        oldKey = NormalizeKey(oldKey);
        newKey = NormalizeKey(newKey);
        if(newKey == KeyCode.None || IsMouseKey(newKey)) return;
        List<int> keys = [.. Conf.AllowedKeys];
        int index = keys.IndexOf((int)oldKey);
        if(index < 0) {
            ToggleAllowedKey(newKey);
            return;
        }
        if(keys.Contains((int)newKey)) {
            keys.RemoveAt(index);
        } else {
            keys[index] = (int)newKey;
        }
        Conf.AllowedKeys = [.. keys];
        PersistChange();
    }
    private static void PersistChange() {
        Save();
        Changed?.Invoke();
    }
    private static Ticker ticker;
    private static void EnsureTicker() {
        if(ticker != null || MainCore.Root == null) return;
        ticker = MainCore.Root.AddComponent<Ticker>();
    }
    private static KeyCode[] captureCandidates;
    private static KeyCode[] CaptureCandidates {
        get {
            if(captureCandidates != null) return captureCandidates;
            List<KeyCode> list = [];
            foreach(KeyCode key in Enum.GetValues(typeof(KeyCode))) {
                if(key == KeyCode.None || IsMouseKey(key) || key >= KeyCode.JoystickButton0) continue;
                list.Add(key);
            }
            captureCandidates = [.. list];
            return captureCandidates;
        }
    }
}
