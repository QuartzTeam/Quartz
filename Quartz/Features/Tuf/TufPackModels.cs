#nullable enable
namespace Quartz.Features.Tuf;

public enum TufPackSort { Recent, Name, Levels }
public enum TufPackLevelSort { PackOrder, Difficulty, Clears }
public enum TufPackListState { Idle, Loading, Ready, Empty, Error }

// One node of a pack's item tree: either a folder (Children) or a level. Mirrors
// the site's pack view, which nests levels in reorderable folders.
public sealed class TufPackItem {
    public long Key { get; }
    public string Name { get; }
    public TufLevel? Level { get; }
    public IReadOnlyList<TufPackItem> Children { get; }
    public int LevelCount { get; }
    public bool IsFolder => Level == null;

    public TufPackItem(long key, TufLevel level) {
        Key = key;
        Name = level.Song;
        Level = level;
        Children = Array.Empty<TufPackItem>();
        LevelCount = 1;
    }

    public TufPackItem(long key, string name, IReadOnlyList<TufPackItem> children) {
        Key = key;
        Name = name;
        Children = children;
        LevelCount = children.Sum(c => c.LevelCount);
    }
}

// A pack summary from the packs list. Levels are loaded on demand when the pack
// is opened (see TufPackService.OpenPack); the list response only carries a small
// preview, so LevelCount is authoritative for the "N levels" label.
// Pack ids are opaque short strings (link codes like "RCAXIAv9"), NOT ints.
public sealed class TufPack {
    public string Id { get; }
    public string Name { get; }
    public string Owner { get; }
    public int LevelCount { get; }
    public int Favorites { get; }
    public IReadOnlyList<string> Preview { get; }
    // Full CDN url of the pack's uploaded icon, straight from the list response; empty
    // when the pack has none. Used for the blurred card background (see TufPreviewCache).
    public string IconUrl { get; set; } = "";
    // First previewed level's id, 0 if none. Most packs have no icon, so the card falls
    // back to this level's video thumbnail.
    public int FirstLevelId { get; set; }

    public TufPack(string id, string name, string owner, int levelCount, int favorites, IReadOnlyList<string> preview) {
        Id = id;
        Name = name;
        Owner = owner;
        LevelCount = levelCount;
        Favorites = favorites;
        Preview = preview;
    }
}

public sealed class TufPacksPage {
    public IReadOnlyList<TufPack> Results { get; }
    public int Total { get; }
    public TufPacksPage(IReadOnlyList<TufPack> results, int total) {
        Results = results ?? Array.Empty<TufPack>();
        Total = total;
    }
}

// diffId -> display name + color + sort rank, fetched once from
// /v2/database/difficulties. Pack tree items only carry diffId, so this resolves
// the label the level cards show and the rank the difficulty sort orders by.
public sealed class TufDifficultyDictionary {
    private readonly Dictionary<int, (string Name, string Color, int Rank)> map;
    public TufDifficultyDictionary(Dictionary<int, (string, string, int)> map) => this.map = map;
    public static TufDifficultyDictionary Empty => new(new());

    public (string Name, string Color) Resolve(int diffId) =>
        map.TryGetValue(diffId, out (string Name, string Color, int Rank) value)
            ? (value.Name, value.Color) : ("Unranked", "#FFFFFF");
    public int RankOf(int diffId) =>
        map.TryGetValue(diffId, out (string Name, string Color, int Rank) value) ? value.Rank : 0;
}
