using System;
using System.Reflection;
using UnityEngine;
using Quartz.Core;
namespace Quartz.Compat.Game;
internal static class FileDialog {
    private static bool resolved;
    private static MethodInfo pickFile;
    private static MethodInfo pickFolder;
    private static MethodInfo saveFile;
    private static MethodInfo reveal;
    private static MethodInfo sfbOpenFile;
    private static MethodInfo sfbOpenFolder;
    private static MethodInfo sfbSaveFile;
    private static Type sfbExtensionFilter;
    public static bool Available {
        get {
            Resolve();
            return pickFile != null || pickFolder != null || sfbOpenFile != null || sfbOpenFolder != null;
        }
    }
    public static string PickFile(string startPath, string filterName, string[] extensions, string title) {
        Resolve();
        if(pickFile != null)
            return Refl.Invoke(pickFile, null, startPath, filterName, extensions, title) as string;
        if(sfbOpenFile != null)
            return First(Refl.Invoke(
                sfbOpenFile, null, title, startPath, Filters(filterName, extensions), false));
        return Unavailable(nameof(PickFile));
    }
    public static string PickFolder(string startPath, string filterName, string[] extensions, string title) {
        Resolve();
        if(pickFolder != null)
            return Refl.Invoke(pickFolder, null, startPath, filterName, extensions, title) as string;
        if(sfbOpenFolder != null)
            return First(Refl.Invoke(sfbOpenFolder, null, title, startPath, false));
        return Unavailable(nameof(PickFolder));
    }
    public static string SaveFile(string startPath, string defaultName, string filterName, string[] extensions, string title) {
        Resolve();
        if(saveFile != null)
            return Refl.Invoke(saveFile, null, startPath, defaultName, filterName, extensions, title) as string;
        if(sfbSaveFile != null)
            return Refl.Invoke(
                sfbSaveFile, null, title, startPath, defaultName, Filters(filterName, extensions)) as string;
        return Unavailable(nameof(SaveFile));
    }
    public static bool Reveal(string path) {
        Resolve();
        if(reveal != null) return Refl.Invoke(reveal, null, path) is bool b && b;
        if(string.IsNullOrEmpty(path)) return false;
        try {
            Application.OpenURL(new Uri(path).AbsoluteUri);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Compat] Reveal fallback failed for '{path}': {e.Message}");
            return false;
        }
    }
    private static string First(object result) =>
        result is string[] { Length: > 0 } paths ? paths[0] : null;
    private static object Filters(string filterName, string[] extensions) {
        if(sfbExtensionFilter == null || extensions == null || extensions.Length == 0) return null;
        try {
            object filter = Activator.CreateInstance(
                sfbExtensionFilter, filterName ?? "", extensions);
            Array all = Array.CreateInstance(sfbExtensionFilter, 1);
            all.SetValue(filter, 0);
            return all;
        } catch {
            return null;
        }
    }
    private static string Unavailable(string what) {
        MainCore.Log.Wrn(
            $"[Compat] {what} is unavailable: this game build ({GameVersion.DisplayRelease}) exposes no "
            + "native file dialog. Copy the file into the Quartz folder by hand instead.");
        return null;
    }
    private static void Resolve() {
        if(resolved) return;
        resolved = true;
        ResolveUnityFileDialog();
        if(pickFile == null || pickFolder == null || saveFile == null) ResolveStandaloneFileBrowser();
    }
    private static void ResolveUnityFileDialog() {
        Type browser = null;
        try {
            browser = Assembly.Load("UnityFileDialog")?.GetType("UnityFileDialog.FileBrowser");
        } catch {
            browser = null;
        }
        if(browser == null) {
            try {
                foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
                    if(a.GetName().Name != "UnityFileDialog") continue;
                    browser = a.GetType("UnityFileDialog.FileBrowser");
                    break;
                }
            } catch {
                browser = null;
            }
        }
        if(browser == null) return;
        pickFile = Refl.Method(browser, "PickFile", 4);
        pickFolder = Refl.Method(browser, "PickFolder", 4);
        saveFile = Refl.Method(browser, "SaveFile", 5);
        reveal = Refl.Method(browser, "Reveal", 1);
    }
    private static void ResolveStandaloneFileBrowser() {
        try {
            Assembly firstpass = Assembly.Load("Assembly-CSharp-firstpass");
            Type sfb = firstpass?.GetType("SFB.StandaloneFileBrowser");
            sfbExtensionFilter = firstpass?.GetType("SFB.ExtensionFilter");
            if(sfb == null || sfbExtensionFilter == null) return;
            sfbOpenFile = Overload(sfb, "OpenFilePanel", 4, 2, sfbExtensionFilter.MakeArrayType());
            sfbOpenFolder = Refl.Method(sfb, "OpenFolderPanel", 3);
            sfbSaveFile = Overload(sfb, "SaveFilePanel", 4, 3, sfbExtensionFilter.MakeArrayType());
        } catch {
            sfbOpenFile = null;
            sfbOpenFolder = null;
            sfbSaveFile = null;
        }
    }
    private static MethodInfo Overload(Type owner, string name, int argCount, int index, Type wanted) {
        foreach(MethodInfo m in owner.GetMethods(Refl.Any)) {
            if(m.Name != name) continue;
            ParameterInfo[] ps = m.GetParameters();
            if(ps.Length == argCount && ps[index].ParameterType == wanted) return m;
        }
        return null;
    }
}
