using System;
using System.Reflection;
using TMPro;
namespace Quartz.Compat.Game;
internal static class TextCompat {
    private const int WrapNoWrap = 0;
    private const int WrapNormal = 1;
    private static bool resolved;
    private static PropertyInfo modeProp;
    private static Type modeType;
    private static PropertyInfo legacyProp;
    public static void SetWrap(TMP_Text text, bool wrap) {
        if(text == null) return;
        Resolve();
        if(modeProp != null && modeType != null) {
            try {
                modeProp.SetValue(text, Enum.ToObject(modeType, wrap ? WrapNormal : WrapNoWrap), null);
                return;
            } catch {
            }
        }
        try {
            legacyProp?.SetValue(text, wrap, null);
        } catch {
        }
    }
    public static object CaptureWrap(TMP_Text text) {
        if(text == null) return null;
        Resolve();
        try {
            if(modeProp != null) return modeProp.GetValue(text, null);
            return legacyProp?.GetValue(text, null);
        } catch {
            return null;
        }
    }
    public static void RestoreWrap(TMP_Text text, object captured) {
        if(text == null || captured == null) return;
        Resolve();
        try {
            if(modeProp != null && modeType != null && captured.GetType() == modeType) {
                modeProp.SetValue(text, captured, null);
                return;
            }
            if(legacyProp != null && captured is bool b) legacyProp.SetValue(text, b, null);
        } catch {
        }
    }
    public static bool GetWrap(TMP_Text text) {
        if(text == null) return true;
        Resolve();
        try {
            if(modeProp != null) return Convert.ToInt32(modeProp.GetValue(text, null)) != WrapNoWrap;
            return legacyProp?.GetValue(text, null) is not bool b || b;
        } catch {
            return true;
        }
    }
    public static void NoWrap(TMP_Text text) => SetWrap(text, false);
    public static void Wrap(TMP_Text text) => SetWrap(text, true);
    private static void Resolve() {
        if(resolved) return;
        resolved = true;
        try {
            modeProp = typeof(TMP_Text).GetProperty("textWrappingMode", Refl.Any);
            modeType = modeProp?.PropertyType;
            if(modeType != null && !modeType.IsEnum) {
                modeProp = null;
                modeType = null;
            }
            legacyProp = typeof(TMP_Text).GetProperty("enableWordWrapping", Refl.Any);
        } catch {
            modeProp = null;
            modeType = null;
            legacyProp = null;
        }
    }
}
