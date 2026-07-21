using System;
using System.Reflection;
namespace Quartz.Compat.Game;
public static class GameVersion {
    public const int LastLegacyRelease = 136;
    private static bool resolved;
    private static int release;
    public static int Release {
        get {
            if(!resolved) Resolve();
            return release;
        }
    }
    public static bool IsLegacy => Release != 0 && Release <= LastLegacyRelease;
    public static string DisplayRelease => Release == 0 ? "r?" : "r" + Release;
    private static void Resolve() {
        resolved = true;
        try {
            Type gcns = typeof(ADOBase).Assembly.GetType("GCNS");
            FieldInfo f = gcns?.GetField("releaseNumber", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if(f == null) return;
            object raw = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
            if(raw is int i) release = i;
        } catch {
            release = 0;
        }
    }
}
