using Quartz.Core;
using System.Reflection;
using UnityEngine;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.Resource;
public static partial class FontManager {
    public const string DefaultName = "Default (Cookie Run Bold)";
    private const string DefaultFontFile = "Cookie Run Bold";
    public const string AddSentinel = "quartz-add-custom-font";
    public const string SameAsOverlay = "quartz-overlay-font-same";
    public static TMP_FontAsset Current { get; private set; }
    public static string CurrentName { get; private set; } = DefaultName;
    public static Transform MenuRoot { get; set; }
    public static TMP_FontAsset MenuFontAsset {
        get {
            string name = MainCore.Conf?.SettingsFontName;
            return string.IsNullOrEmpty(name) || name == SameAsOverlay ? Current : GetFont(name);
        }
    }
    public static event Action OnFontChanged;
    public static event Action OnFontCatalogChanged;
    private static TMP_FontAsset defaultFont;
    private static Font defaultSourceFont;
    private static TMP_FontAsset cjkFallback;
    private static readonly int[] CjkProbe = { 0x8FD9, 0x56FD, 0x8BF4, 0x8BED, 0x95E8 };
    private static readonly Dictionary<string, TMP_FontAsset> cache = [];
    private static readonly List<Font> sourceFonts = [];
    private static readonly Dictionary<string, Font> sourceByName = [];
    private static readonly Dictionary<string, string> fontFiles = [];
    private static readonly HashSet<string> customNames = new(StringComparer.OrdinalIgnoreCase);
    private static string[] available;
    private static bool scanned;
    public static void Initialize() {
        bool primed = EnsureTmpSettings();
        try {
            defaultFont = BuildDefaultFont();
            Current = defaultFont;
            CurrentName = DefaultName;
            string saved = MainCore.Conf.FontName;
            if(!string.IsNullOrEmpty(saved) && saved != DefaultName && saved != DefaultFontFile) SetFont(saved, false);
        } finally {
            if(primed) ReleaseTmpSettings();
        }
    }
    private static FieldInfo tmpSettingsInstanceField;
    private static TMP_Settings tmpSettingsFallback;
    private static bool EnsureTmpSettings() {
        try {
            if(TMP_Settings.instance != null) return false;
        } catch {
        }
        try {
            tmpSettingsInstanceField ??= typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            if(tmpSettingsInstanceField == null) return false;
            tmpSettingsFallback = ScriptableObject.CreateInstance<TMP_Settings>();
            tmpSettingsInstanceField.SetValue(null, tmpSettingsFallback);
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] couldn't prime TMP_Settings: {e.Message}");
            return false;
        }
    }
    private static void ReleaseTmpSettings() {
        try {
            tmpSettingsInstanceField?.SetValue(null, null);
            if(tmpSettingsFallback != null) UnityEngine.Object.Destroy(tmpSettingsFallback);
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] couldn't release primed TMP_Settings: {e.Message}");
        } finally {
            tmpSettingsFallback = null;
        }
    }
    private static TMP_FontAsset BuildDefaultFont() {
        EnsureScanned();
        if(fontFiles.TryGetValue(DefaultFontFile, out string path)) {
            Font font = null;
            try {
                font = new Font(path);
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                asset.isMultiAtlasTexturesEnabled = true;
                AttachCjk(asset);
                defaultSourceFont = font;
                return asset;
            } catch(Exception e) {
                if(font != null) UnityEngine.Object.Destroy(font);
                MainCore.Log.Wrn($"[FontManager] default '{DefaultFontFile}' build failed: {e.Message}");
            }
        }
        defaultSourceFont = null;
        TMP_FontAsset suit = MainCore.Res.Get<TMP_FontAsset>(Asset.SUIT_Medium);
        AttachCjk(suit);
        return suit;
    }
    public static IReadOnlyList<string> GetAvailableFonts() {
        if(available != null) return available;
        EnsureScanned();
        var list = new List<string> { DefaultName };
        list.AddRange(fontFiles.Keys
            .Where(n => !string.Equals(n, DefaultFontFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        available = [.. list];
        return available;
    }
    public static bool IsCustomFont(string name) {
        if(string.IsNullOrEmpty(name) || name == DefaultName || name == AddSentinel) return false;
        EnsureScanned();
        return customNames.Contains(name);
    }
    private static void EnsureScanned() {
        if(!scanned) ScanFontFiles();
    }
    private static void ScanFontFiles() {
        fontFiles.Clear();
        customNames.Clear();
        ScanDir(MainCore.Paths.FontPath, false);
        ScanDir(MainCore.Paths.CustomFontPath, true);
        scanned = true;
    }
    private static void ScanDir(string dir, bool custom) {
        try {
            if(!Directory.Exists(dir)) return;
            foreach(string path in Directory.GetFiles(dir)) {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if(ext != ".ttf" && ext != ".otf" && ext != ".ttc") continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if(string.IsNullOrWhiteSpace(name)) continue;
                fontFiles[name] = path;
                if(custom) customNames.Add(name);
                else customNames.Remove(name);
            }
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] font scan failed: {e.Message}");
        }
    }
    public static void SetFont(string name, bool save) {
        TMP_FontAsset asset = Resolve(name);
        if(asset == null) {
            asset = defaultFont;
            name = DefaultName;
        }
        Current = asset;
        CurrentName = name;
        ApplyToAll();
        if(save) {
            MainCore.Conf.FontName = name == DefaultName ? "" : name;
            MainCore.ConfMgr.RequestSave();
        }
        OnFontChanged?.Invoke();
    }
    public static string ImportFont(string srcPath) {
        if(string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath)) return null;
        string ext = Path.GetExtension(srcPath).ToLowerInvariant();
        if(ext != ".ttf" && ext != ".otf" && ext != ".ttc") return null;
        try {
            string dir = MainCore.Paths.CustomFontPath;
            Directory.CreateDirectory(dir);
            string baseName = Sanitize(Path.GetFileNameWithoutExtension(srcPath)) ?? "Font";
            string name = UniqueName(baseName);
            File.Copy(srcPath, Path.Combine(dir, name + ext), false);
            Invalidate();
            OnFontCatalogChanged?.Invoke();
            return name;
        } catch(Exception e) {
            MainCore.Log.Err($"[FontManager] import failed: {e.Message}");
            return null;
        }
    }
    public static bool RenameFont(string oldName, string newName, out string error) {
        error = null;
        EnsureScanned();
        if(!customNames.Contains(oldName) || !fontFiles.TryGetValue(oldName, out string oldPath)) {
            error = "Not a custom font.";
            return false;
        }
        string clean = Sanitize(newName);
        if(clean == null) {
            error = "Enter a valid name.";
            return false;
        }
        if(string.Equals(clean, oldName, StringComparison.Ordinal)) return true;
        if(fontFiles.ContainsKey(clean)) {
            error = "That name is already used.";
            return false;
        }
        string ext = Path.GetExtension(oldPath);
        string newPath = Path.Combine(MainCore.Paths.CustomFontPath, clean + ext);
        bool wasCurrent = CurrentName == oldName;
        EvictCache(oldName);
        try {
            File.Move(oldPath, newPath);
        } catch(Exception e) {
            error = e.Message;
            MainCore.Log.Err($"[FontManager] rename failed: {e.Message}");
            Invalidate();
            if(wasCurrent) {
                SetFont(oldName, false);
            }
            return false;
        }
        Invalidate();
        if(wasCurrent) SetFont(clean, true);
        RetargetFontOverrides(oldName, clean);
        OnFontCatalogChanged?.Invoke();
        return true;
    }
    public static bool DeleteFont(string name) {
        EnsureScanned();
        if(!customNames.Contains(name) || !fontFiles.TryGetValue(name, out string path)) return false;
        bool wasCurrent = CurrentName == name;
        EvictCache(name);
        try {
            File.Delete(path);
        } catch(Exception e) {
            MainCore.Log.Err($"[FontManager] delete failed: {e.Message}");
            Invalidate();
            if(wasCurrent) {
                SetFont(name, false);
            }
            return false;
        }
        Invalidate();
        if(wasCurrent) SetFont(DefaultName, true);
        RetargetFontOverrides(name, "");
        OnFontCatalogChanged?.Invoke();
        return true;
    }
    private static void RetargetFontOverrides(string oldName, string replacement) {
        var conf = MainCore.Conf;
        if(conf == null) return;
        if(string.Equals(conf.SettingsFontName, oldName, StringComparison.OrdinalIgnoreCase)) {
            conf.SettingsFontName = replacement;
            ApplyMenuFont();
            MainCore.ConfMgr.RequestSave();
        }
    }
    private static string UniqueName(string baseName) {
        EnsureScanned();
        string name = baseName;
        int n = 2;
        while(fontFiles.ContainsKey(name)) {
            name = $"{baseName} ({n})";
            n++;
        }
        return name;
    }
    private static string Sanitize(string s) {
        if(string.IsNullOrWhiteSpace(s)) return null;
        foreach(char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, ' ');
        s = s.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
    private static void EvictCache(string name) {
        if(cache.TryGetValue(name, out TMP_FontAsset asset)) {
            DestroyFontAsset(asset);
            cache.Remove(name);
        }
        if(sourceByName.TryGetValue(name, out Font source)) {
            sourceFonts.Remove(source);
            if(source != null) {
                UnityEngine.Object.DestroyImmediate(source);
            }
            sourceByName.Remove(name);
        }
    }
    private static void Invalidate() {
        available = null;
        scanned = false;
        fontFiles.Clear();
        customNames.Clear();
    }
    public static TMP_FontAsset GetFont(string name) => Resolve(name) ?? defaultFont;
    public static void ApplyToAll() {
        if(MainCore.Root == null || Current == null) return;
        TMP_FontAsset menuFont = MenuFontAsset ?? Current;
        AttachCjk(Current);
        if(menuFont != Current) AttachCjk(menuFont);
        Transform menuRoot = MenuRoot;
        TMP_Text[] texts = MainCore.Root.GetComponentsInChildren<TMP_Text>(true);
        for(int i = 0; i < texts.Length; i++) {
            TMP_Text text = texts[i];
            if(text == null || text.GetComponent<FontExempt>() != null) continue;
            bool isMenu = menuRoot != null
                && (text.transform == menuRoot || text.transform.IsChildOf(menuRoot));
            text.font = isMenu ? menuFont : Current;
        }
    }
    public static void ApplyMenuFont() {
        Transform menuRoot = MenuRoot;
        if(menuRoot == null) return;
        TMP_FontAsset menuFont = MenuFontAsset ?? Current;
        if(menuFont == null) return;
        AttachCjk(menuFont);
        TMP_Text[] texts = menuRoot.GetComponentsInChildren<TMP_Text>(true);
        for(int i = 0; i < texts.Length; i++) {
            TMP_Text text = texts[i];
            if(text == null || text.GetComponent<FontExempt>() != null) continue;
            text.font = menuFont;
            text.ForceMeshUpdate(false, true);
        }
    }
    private static void AttachCjk(TMP_FontAsset asset) {
        if(asset == null) return;
        TMP_FontAsset fb = GetCjkFallback();
        if(fb == null || fb == asset) return;
        asset.fallbackFontAssetTable ??= [];
        if(!asset.fallbackFontAssetTable.Contains(fb)) asset.fallbackFontAssetTable.Add(fb);
    }
    private static TMP_FontAsset GetCjkFallback() {
        if(cjkFallback != null) return cjkFallback;
        try {
            TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for(int pass = 0; pass < 2 && cjkFallback == null; pass++) {
                bool tryAdd = pass == 1;
                foreach(TMP_FontAsset f in all) {
                    if(f == null || f == defaultFont || cache.ContainsValue(f)) continue;
                    try {
                        int hit = 0;
                        foreach(int cp in CjkProbe)
                            if(f.HasCharacter((char)cp, false, tryAdd)) hit++;
                        if(hit >= 3) { cjkFallback = f; break; }
                    } catch { }
                }
            }
            if(cjkFallback != null) MainCore.Log.Msg($"[FontManager] CJK fallback: {cjkFallback.name}");
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] CJK fallback probe failed: {e.Message}");
        }
        return cjkFallback;
    }
    private static TMP_FontAsset Resolve(string name) {
        if(string.IsNullOrEmpty(name) || name == DefaultName || name == AddSentinel) return defaultFont;
        if(cache.TryGetValue(name, out TMP_FontAsset cached)) return cached;
        EnsureScanned();
        if(!fontFiles.TryGetValue(name, out string path)) return null;
        Font font = null;
        try {
            font = new Font(path);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            asset.isMultiAtlasTexturesEnabled = true;
            AttachCjk(asset);
            cache[name] = asset;
            sourceFonts.Add(font);
            sourceByName[name] = font;
            return asset;
        } catch(Exception e) {
            if(font != null) UnityEngine.Object.Destroy(font);
            MainCore.Log.Wrn($"[FontManager] build '{name}' failed: {e.Message}");
            return null;
        }
    }
    public static void Dispose() {
        foreach(TMP_FontAsset asset in cache.Values) DestroyFontAsset(asset);
        cache.Clear();
        foreach(Font font in sourceFonts) if(font != null) UnityEngine.Object.Destroy(font);
        sourceFonts.Clear();
        sourceByName.Clear();
        if(defaultSourceFont != null) {
            DestroyFontAsset(defaultFont);
            UnityEngine.Object.Destroy(defaultSourceFont);
            defaultSourceFont = null;
        }
        defaultFont = null;
        Current = null;
        CurrentName = DefaultName;
        cjkFallback = null;
        fontFiles.Clear();
        customNames.Clear();
        available = null;
        scanned = false;
    }
    private static void DestroyFontAsset(TMP_FontAsset asset) {
        if(asset == null) return;
        Material assetMaterial = GameApi.FontMaterial(asset);
        if(assetMaterial != null) UnityEngine.Object.Destroy(assetMaterial);
        Texture2D[] atlases = asset.atlasTextures;
        if(atlases != null)
            foreach(Texture2D tex in atlases) if(tex != null) UnityEngine.Object.Destroy(tex);
        UnityEngine.Object.Destroy(asset);
    }
}
