#nullable enable
namespace Quartz.Features.Tuf;

public static class TufApiQuery {
    public static string BuildPath(string query, TufSort sort, bool ascending, int offset,
        TufDifficultyFilter? filter = null) {
        filter ??= TufDifficultyFilter.AllRanked;
        string order = sort switch {
            TufSort.Difficulty => "DIFF", TufSort.Clears => "CLEARS", TufSort.Likes => "LIKES", _ => "RECENT"
        };
        string path = "v2/database/levels?limit=50"
            + "&offset=" + Math.Max(0, offset)
            + "&query=" + Uri.EscapeDataString(TufInput.NormalizeQuery(query))
            + "&pguRange=" + Uri.EscapeDataString(filter.MinName) + "," + Uri.EscapeDataString(filter.MaxName)
            + "&sort=" + order + "_" + (ascending ? "ASC" : "DESC")
            + "&deletedFilter=hide";
        if(filter.SelectedDifficulties.Count > 0) {
            string selected = string.Join(",", filter.SelectedDifficulties);
            path += "&specialDifficulties=" + Uri.EscapeDataString(selected);
        }
        return path;
    }
}
