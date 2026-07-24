using System.Reflection;
using Newtonsoft.Json.Linq;
namespace Quartz.IO;
public sealed class FaqEntry {
    public string Category;
    public string Question;
    public string Answer;
}
public static class FaqDocument {
    public const string FallbackLanguage = "en-US";
    public static List<FaqEntry> Parse(string json, string language) => Parse(JToken.Parse(json), language);
    public static List<FaqEntry> Parse(JToken root, string language) {
        List<FaqEntry> list = [];
        JArray entries = root as JArray ?? root?["entries"] as JArray;
        if(entries == null) return list;
        foreach(JToken token in entries) {
            if(token is not JObject obj) continue;
            string question = Text(obj["question"], language);
            string answer = Text(obj["answer"], language);
            if(string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(answer)) continue;
            list.Add(new FaqEntry {
                Category = Text(obj["category"], language)?.Trim(),
                Question = string.IsNullOrWhiteSpace(question) ? "?" : question.Trim(),
                Answer = answer?.Trim() ?? "",
            });
        }
        return list;
    }
    public static string Text(JToken token, string language) {
        switch(token) {
            case null:
            case JValue { Value: null }:
                return null;
            case JArray array: {
                List<string> lines = [];
                foreach(JToken line in array) {
                    string text = Text(line, language);
                    if(text != null) lines.Add(text);
                }
                return string.Join("\n", lines);
            }
            case JObject obj: {
                if(!string.IsNullOrEmpty(language) && obj[language] != null) return Text(obj[language], language);
                if(obj[FallbackLanguage] != null) return Text(obj[FallbackLanguage], language);
                foreach(JProperty property in obj.Properties()) return Text(property.Value, language);
                return null;
            }
            default:
                return token.ToString();
        }
    }
    private const string DefaultResource = "Quartz.FAQ.Default.json";
    private const string EmptyDocument = "{\"entries\":[]}";
    private static string cachedDefault;
    public static string Default => cachedDefault ??= ReadEmbeddedDefault();
    private static string ReadEmbeddedDefault() {
        try {
            using Stream stream = typeof(FaqDocument).Assembly.GetManifestResourceStream(DefaultResource);
            if(stream == null) return EmptyDocument;
            using StreamReader reader = new(stream);
            string text = reader.ReadToEnd();
            return string.IsNullOrWhiteSpace(text) ? EmptyDocument : text;
        } catch {
            return EmptyDocument;
        }
    }
}
