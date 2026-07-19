#nullable enable
using Newtonsoft.Json.Linq;
using Quartz.IO.Interface;

namespace Quartz.Features.Tuf;

// One record per installed level. Holds enough metadata to render a browser card
// with no network — the whole point of the Installed view is answering "what did I
// download" offline — plus the folder the level actually lives in.
//
// Folder is stored per entry rather than derived from the active root: after a
// library move is interrupted, some levels are at the new root and some are still
// at the old one, and both must stay loadable and deletable.
public sealed class TufInstallEntry {
    public int Id;
    public string Song = "";
    public string Artist = "";
    public string Creator = "";
    public string Difficulty = "";
    public string DifficultyColor = "#FFFFFF";
    public int Clears;
    public int Likes;
    public string Folder = "";
    public string DownloadUrl = "";
    public string VideoLink = "";
    public long InstalledAtUtc;

    public JObject Serialize() => new() {
        [nameof(Id)] = Id,
        [nameof(Song)] = Song,
        [nameof(Artist)] = Artist,
        [nameof(Creator)] = Creator,
        [nameof(Difficulty)] = Difficulty,
        [nameof(DifficultyColor)] = DifficultyColor,
        [nameof(Clears)] = Clears,
        [nameof(Likes)] = Likes,
        [nameof(Folder)] = Folder,
        [nameof(DownloadUrl)] = DownloadUrl,
        [nameof(VideoLink)] = VideoLink,
        [nameof(InstalledAtUtc)] = InstalledAtUtc
    };

    public static TufInstallEntry? Deserialize(JToken token) {
        try {
            int id = token[nameof(Id)]?.Value<int>() ?? 0;
            if(id <= 0) return null;
            string folder = token[nameof(Folder)]?.Value<string>() ?? "";
            if(string.IsNullOrWhiteSpace(folder)) return null;
            return new TufInstallEntry {
                Id = id,
                // Re-run the same display caps the API path uses: the index is a file
                // on disk and may have been hand-edited. Empty stays empty rather than
                // becoming "Unknown" — an adopted level has genuinely unknown metadata,
                // and the browser renders that case as the level id.
                Song = TufInput.CapDisplay(token[nameof(Song)]?.Value<string>(), ""),
                Artist = TufInput.CapDisplay(token[nameof(Artist)]?.Value<string>(), ""),
                Creator = TufInput.CapDisplay(token[nameof(Creator)]?.Value<string>(), ""),
                Difficulty = TufInput.CapDisplay(token[nameof(Difficulty)]?.Value<string>(), "", 24),
                DifficultyColor = TufInput.NormalizeColor(token[nameof(DifficultyColor)]?.Value<string>()),
                Clears = Math.Max(0, token[nameof(Clears)]?.Value<int>() ?? 0),
                Likes = Math.Max(0, token[nameof(Likes)]?.Value<int>() ?? 0),
                Folder = folder,
                DownloadUrl = token[nameof(DownloadUrl)]?.Value<string>() ?? "",
                VideoLink = TufInput.CapDisplay(token[nameof(VideoLink)]?.Value<string>(), "", 300),
                InstalledAtUtc = token[nameof(InstalledAtUtc)]?.Value<long>() ?? 0
            };
        } catch { return null; }
    }

    public TufLevel ToLevel() {
        Uri? uri = Uri.TryCreate(DownloadUrl, UriKind.Absolute, out Uri? parsed)
            && TufNetworkPolicy.IsAllowedDownloadUri(parsed) ? parsed : null;
        return new TufLevel(Id, Song, Artist, Creator, Difficulty, DifficultyColor, Clears, Likes, uri) {
            VideoLink = VideoLink
        };
    }
}

// Newest install first. Main-thread only — every writer marshals through
// MainThread.Enqueue before touching it.
public sealed class TufInstallIndex : ISettingsFile {
    private readonly List<TufInstallEntry> entries = [];

    public IReadOnlyList<TufInstallEntry> Entries => entries;
    public int Count => entries.Count;

    public TufInstallEntry? Find(int id) => entries.FirstOrDefault(e => e.Id == id);

    public void Record(TufLevel level, string folder) {
        if(level == null || level.Id <= 0 || string.IsNullOrWhiteSpace(folder)) return;
        TufInstallEntry? existing = Find(level.Id);
        // Keep the original install time on a re-download so the Installed view does
        // not reshuffle when a level is repaired.
        long installedAt = existing?.InstalledAtUtc ?? 0;
        if(installedAt <= 0) installedAt = DateTime.UtcNow.Ticks;
        if(existing != null) entries.Remove(existing);
        entries.Insert(0, new TufInstallEntry {
            Id = level.Id,
            Song = level.Song,
            Artist = level.Artist,
            Creator = level.Creator,
            Difficulty = level.Difficulty,
            DifficultyColor = level.DifficultyColor,
            Clears = level.Clears,
            Likes = level.Likes,
            Folder = Path.GetFullPath(folder),
            DownloadUrl = level.DownloadUri?.ToString() ?? "",
            VideoLink = level.VideoLink,
            InstalledAtUtc = installedAt
        });
        Sort();
    }

    // Adopts a level folder that exists on disk but has no record — installs from
    // before this index shipped, or ones TUFHelperLite made. Metadata is unknown, so
    // the card shows the id until the level turns up in a search again.
    public TufInstallEntry Adopt(int id, string folder, long installedAtUtc) {
        TufInstallEntry entry = new() {
            Id = id,
            Song = "",
            Artist = "",
            Creator = "",
            Difficulty = "",
            Folder = Path.GetFullPath(folder),
            InstalledAtUtc = installedAtUtc
        };
        entries.Add(entry);
        Sort();
        return entry;
    }

    public bool Remove(int id) {
        TufInstallEntry? entry = Find(id);
        if(entry == null) return false;
        entries.Remove(entry);
        return true;
    }

    public void SetFolder(int id, string folder) {
        TufInstallEntry? entry = Find(id);
        if(entry != null && !string.IsNullOrWhiteSpace(folder)) entry.Folder = Path.GetFullPath(folder);
    }

    // Drops records whose folder is gone (deleted outside the game) so the Installed
    // view never offers to load something that is not there.
    public bool PruneMissing() {
        int removed = entries.RemoveAll(e => {
            try { return !Directory.Exists(e.Folder); } catch { return true; }
        });
        return removed > 0;
    }

    private void Sort() => entries.Sort((a, b) => b.InstalledAtUtc.CompareTo(a.InstalledAtUtc));

    public JToken Serialize() => new JObject {
        ["Version"] = 1,
        ["Entries"] = new JArray(entries.Select(e => e.Serialize()).Cast<object>().ToArray())
    };

    public void Deserialize(JToken token) {
        entries.Clear();
        if(token["Entries"] is not JArray array) return;
        HashSet<int> seen = [];
        foreach(JToken item in array) {
            TufInstallEntry? entry = TufInstallEntry.Deserialize(item);
            if(entry != null && seen.Add(entry.Id)) entries.Add(entry);
        }
        Sort();
    }
}
