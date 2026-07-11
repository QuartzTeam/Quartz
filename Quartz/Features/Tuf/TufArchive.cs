#nullable enable
using System.IO.Compression;
using System.Text;

namespace Quartz.Features.Tuf;

public static class TufArchive {
    public const int MaxEntries = 10_000;
    public const long MaxEntryBytes = 512L * 1024 * 1024;
    public const long MaxOutputBytes = 1024L * 1024 * 1024;

    // Legacy code pages tried, in order, to recover mojibake entry names from zips
    // that stored filenames in a national encoding without the UTF-8 flag. Korean
    // (949) first — most ADOFAI charts come from there — then Japanese, GBK, Big5,
    // Windows-1252. UTF-8 is tried before any of these.
    private static readonly int[] LegacyCodePages = { 949, 932, 936, 950, 1252 };

    // Returns the number of entries skipped because a single asset could not be
    // decompressed or written; the caller decides whether a playable chart survived.
    public static int Extract(string archivePath, string destination) {
        string root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? root : root + Path.DirectorySeparatorChar;
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        if(archive.Entries.Count > MaxEntries) throw new InvalidDataException("Archive contains too many entries.");
        long total = 0;
        int skipped = 0;
        foreach(ZipArchiveEntry entry in archive.Entries) {
            if(IsSymlink(entry)) throw new InvalidDataException("Archive contains a symbolic link.");
            string original = RecoverEntryName(entry.FullName);
            string relative = NormalizeEntry(original);
            if(string.IsNullOrEmpty(relative)) continue;
            string target;
            try { target = Path.GetFullPath(Path.Combine(root, relative)); }
            catch(Exception e) when(IsSkippableEntryError(e)) { skipped++; continue; }
            if(!target.StartsWith(prefix, PathComparison)) throw new InvalidDataException("Archive path escapes level folder.");
            if(original.EndsWith('/') || original.EndsWith('\\') || string.IsNullOrEmpty(entry.Name)) {
                Directory.CreateDirectory(target);
                continue;
            }
            long size = entry.Length;
            if(size < 0 || size > MaxEntryBytes || total > MaxOutputBytes - size)
                throw new InvalidDataException("Archive expands beyond the safe size limit.");
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using Stream input = entry.Open();
                using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                CopyBounded(input, output, size);
                total += size;
            } catch(Exception e) when(IsSkippableEntryError(e)) {
                // One asset we cannot decompress (a zip method System.IO.Compression
                // lacks, e.g. Deflate64) or write (a name illegal on this filesystem)
                // must not doom the whole level — the .adofai chart almost always
                // extracts fine. Drop it and let SelectChart judge what remains.
                skipped++;
                TryDelete(target);
            }
        }
        return skipped;
    }

    // Level zips usually wrap everything in a single "Artist - Title" folder; hoist
    // that wrapper's contents so the chart sits directly in Levels/<id>/. Runs a few
    // levels deep for double-wrapped archives. macOS zip junk (__MACOSX, .DS_Store)
    // is ignored when deciding whether a single wrapper exists.
    public static void FlattenSingleRoot(string destination) {
        string root = Path.GetFullPath(destination);
        for(int depth = 0; depth < 4; depth++) {
            if(!Directory.Exists(root)) return;
            string[] dirs = Directory.GetDirectories(root)
                .Where(d => !string.Equals(Path.GetFileName(d), "__MACOSX", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            bool anyRealFile = Directory.GetFiles(root).Any(f =>
                Path.GetFileName(f) is not (".DS_Store" or "Thumbs.db" or "desktop.ini"));
            if(dirs.Length != 1 || anyRealFile) return;
            string wrapper = dirs[0];
            if((File.GetAttributes(wrapper) & FileAttributes.ReparsePoint) != 0) return;
            // Rename the wrapper out of the way first so a child that shares its
            // name cannot collide while its contents move up.
            string staging = Path.Combine(root, ".flatten-" + Guid.NewGuid().ToString("N"));
            Directory.Move(wrapper, staging);
            foreach(string child in Directory.GetFileSystemEntries(staging)) {
                string target = Path.Combine(root, Path.GetFileName(child));
                if(Directory.Exists(child)) Directory.Move(child, target);
                else File.Move(child, target);
            }
            Directory.Delete(staging);
        }
    }

    // Every playable chart under the level root, in launch-preference order:
    // main.adofai, then the archive-named chart, then largest-first (name-tiebreak).
    // SelectChart is simply the head of this list, so both stay in sync.
    public static IReadOnlyList<string> ListCharts(string? root, string? archiveStem = null) {
        if(string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return Array.Empty<string>();
        string rootFull = Path.GetFullPath(root);
        if((File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) != 0) return Array.Empty<string>();
        List<FileInfo> candidates = EnumerateFilesSafely(rootFull)
            .Where(f => string.Equals(f.Extension, ".adofai", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Name.Contains("backup", StringComparison.OrdinalIgnoreCase))
            .Where(f => IsChartUnderRoot(f.FullName, rootFull))
            .ToList();
        if(candidates.Count == 0) return Array.Empty<string>();
        string? wanted = string.IsNullOrWhiteSpace(archiveStem) ? null : SafeStem(archiveStem) + ".adofai";
        return candidates
            .OrderByDescending(f => string.Equals(f.Name, "main.adofai", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => wanted != null && string.Equals(f.Name, wanted, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Length)
            .ThenBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(f => f.FullName)
            .ToList();
    }

    public static string? SelectChart(string? root, string? archiveStem = null) =>
        ListCharts(root, archiveStem).FirstOrDefault();

    public static bool IsChartUnderRoot(string? path, string? root) {
        if(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)
            || !string.Equals(Path.GetExtension(path), ".adofai", StringComparison.OrdinalIgnoreCase)) return false;
        string rootFull = Path.GetFullPath(root);
        string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? rootFull : rootFull + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if(!full.StartsWith(prefix, PathComparison) || !File.Exists(full)) return false;
        string? current = full;
        while(current != null && !string.Equals(current, rootFull, PathComparison)) {
            if((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            current = Path.GetDirectoryName(current);
        }
        if(current == null) return false;
        return (File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) == 0;
    }

    private static string NormalizeEntry(string name) {
        if(string.IsNullOrEmpty(name)) return "";
        string value = name.Replace('\\', '/');
        if(value.StartsWith('/') || (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'))
            throw new InvalidDataException("Archive contains an absolute path.");
        string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if(parts.Any(p => p is "." or "..")) throw new InvalidDataException("Archive contains path traversal.");
        return Path.Combine(parts);
    }

    // System.IO.Compression decodes non-UTF8-flagged entry names as UTF-8 with a
    // replacement fallback, so a name stored in a national code page (common for
    // Korean/Japanese charts) arrives as U+FFFD boxes. When every char is <= 0xFF the
    // decode was byte-preserving, so we can reconstruct the original bytes and re-decode
    // them: strict UTF-8 first, then the legacy code pages. Names that already hold real
    // unicode (a UTF-8-flagged entry, char > 0xFF) or plain ASCII are returned untouched.
    private static string RecoverEntryName(string name) {
        if(string.IsNullOrEmpty(name)) return name;
        bool hasHigh = false;
        foreach(char c in name) {
            if(c > 0xFF) return name;
            if(c > 0x7F) hasHigh = true;
        }
        if(!hasHigh) return name;
        byte[] raw = new byte[name.Length];
        for(int i = 0; i < name.Length; i++) raw[i] = (byte)name[i];
        try { return new UTF8Encoding(false, true).GetString(raw); } catch { }
        foreach(int codePage in LegacyCodePages) {
            try {
                string decoded = Encoding.GetEncoding(codePage).GetString(raw);
                if(decoded.IndexOf('\uFFFD') < 0) return decoded;
            } catch { }
        }
        return name;
    }

    private static bool IsSkippableEntryError(Exception e) =>
        e is NotSupportedException or IOException or UnauthorizedAccessException
          or InvalidDataException or ArgumentException or System.Security.SecurityException;

    private static void TryDelete(string path) {
        try { if(File.Exists(path)) File.Delete(path); } catch { }
    }

    private static bool IsSymlink(ZipArchiveEntry entry) {
        int unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode == 0xA000;
    }

    private static void CopyBounded(Stream input, Stream output, long expected) {
        byte[] buffer = new byte[65536];
        long written = 0;
        while(true) {
            int read = input.Read(buffer, 0, buffer.Length);
            if(read == 0) break;
            written += read;
            if(written > expected || written > MaxEntryBytes) throw new InvalidDataException("Archive entry exceeded declared size.");
            output.Write(buffer, 0, read);
        }
        if(written != expected) throw new InvalidDataException("Archive entry size did not match its header.");
    }

    private static string SafeStem(string? value) {
        string stem = Path.GetFileNameWithoutExtension(value ?? "");
        foreach(char c in Path.GetInvalidFileNameChars()) stem = stem.Replace(c.ToString(), "");
        return stem;
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafely(string root) {
        Stack<string> pending = new();
        pending.Push(root);
        int seen = 0;
        while(pending.Count > 0) {
            string directory = pending.Pop();
            foreach(string file in Directory.EnumerateFiles(directory)) {
                if(++seen > MaxEntries) throw new InvalidDataException("Cached level contains too many files.");
                if((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return new FileInfo(file);
            }
            foreach(string child in Directory.EnumerateDirectories(directory)) {
                if(++seen > MaxEntries) throw new InvalidDataException("Cached level contains too many files.");
                if((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
            }
        }
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
