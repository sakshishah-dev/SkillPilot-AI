using System.Text.Json;

namespace SkillPilot.Api.Ai;

public record ExerciseDraft(string Prompt, string StarterCode, string[] Hints, string Rubric, string Solution, string Source);

public record ReviewResult(
    int Score, string Verdict, string Summary, string[] Issues, string[] Suggestions,
    string Complexity, string NextStep, bool Verified, string Source);

public class AiLimitException(int limit) : Exception($"Daily AI limit of {limit} calls reached. It resets tomorrow.");

public static class JsonExtract
{
    /// <summary>Finds the first JSON object in a model reply, even if it is wrapped in text or code fences.</summary>
    public static JsonElement? Object(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            return doc.RootElement.Clone();
        }
        catch (JsonException) { return null; }
    }

    public static string Str(this JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    public static string[] Strs(this JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();

    public static int Int(this JsonElement e, string name, int fallback = 0)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : fallback;
}
