using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Ai;

/// <summary>
/// Used when no AI key is set. It keeps the app usable for the demo but it cannot judge code,
/// so its reviews are marked unverified and never earn high mastery.
/// </summary>
public static class OfflineTutor
{
    private const string Source = "offline";

    private static readonly Dictionary<string, ExerciseDraft> Bank = new()
    {
        ["csharp.collections"] = new("Write a method `CountWords(string text)` that returns a Dictionary<string,int> with how many times each word appears. Ignore case.",
            "public static Dictionary<string,int> CountWords(string text)\n{\n    // your code\n}",
            new[] { "Split the text into words first.", "Use a Dictionary and check ContainsKey before adding.", "Call ToLower() on each word before counting." },
            "Splits on spaces; case insensitive; returns correct counts; handles empty input.",
            "public static Dictionary<string,int> CountWords(string text)\n{\n    var result = new Dictionary<string,int>();\n    foreach (var w in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))\n    {\n        var key = w.ToLower();\n        result[key] = result.GetValueOrDefault(key) + 1;\n    }\n    return result;\n}", Source),
        ["dsa.arrays"] = new("Write `int MaxValue(int[] nums)` that returns the largest number without using LINQ or Array.Max. Return int.MinValue for an empty array.",
            "public static int MaxValue(int[] nums)\n{\n    // your code\n}",
            new[] { "Keep one variable for the biggest value seen so far.", "Start it at int.MinValue.", "Loop once and update it when you see a bigger number." },
            "Single pass; correct for negatives; handles empty array.",
            "public static int MaxValue(int[] nums)\n{\n    var best = int.MinValue;\n    foreach (var n in nums) if (n > best) best = n;\n    return best;\n}", Source),
        ["dsa.hashmaps"] = new("Write `bool HasDuplicate(int[] nums)` that returns true if any number appears twice. Aim for O(n).",
            "public static bool HasDuplicate(int[] nums)\n{\n    // your code\n}",
            new[] { "Remember the numbers you have already seen.", "A HashSet answers 'seen before?' in constant time.", "HashSet.Add returns false when the value already exists." },
            "Uses a HashSet; O(n) time; handles empty array.",
            "public static bool HasDuplicate(int[] nums)\n{\n    var seen = new HashSet<int>();\n    foreach (var n in nums) if (!seen.Add(n)) return true;\n    return false;\n}", Source),
        ["sql.select"] = new("Given a table `employees(id, name, salary, department)`, write a query for the names of employees in 'Sales' earning more than 50000, highest salary first.",
            "-- your query\n",
            new[] { "You need WHERE with two conditions.", "Combine conditions with AND.", "Finish with ORDER BY salary DESC." },
            "Selects name; filters both conditions; orders by salary descending.",
            "SELECT name FROM employees WHERE department = 'Sales' AND salary > 50000 ORDER BY salary DESC;", Source),
    };

    public static ExerciseDraft Exercise(Topic topic)
    {
        if (Bank.TryGetValue(topic.Id, out var d)) return d;
        return new ExerciseDraft(
            $"Write a small {topic.Language} program that shows {topic.Title.ToLower()}. Keep it under 25 lines, add one comment explaining your choice, and include a tiny example call.",
            "// your code\n",
            new[] { "Re-read the key idea of the lesson in one sentence.", "Write the smallest version that works, then improve it.", "Name things clearly and test one normal case and one edge case." },
            "Demonstrates the core idea; runs without errors; handles one edge case.",
            "(No sample solution is stored for this topic in offline mode. Add an AI key to get one.)", Source);
    }

    public static string Answer(Topic topic, string question) =>
        $"**{topic.Title}**\n\n{topic.Description}\n\nYou asked: \"{question}\"\n\n" +
        "I am the offline demo tutor, so I cannot answer free questions yet. Open the resource for this skill, then write down what is still unclear and ask again once an AI key is set in the server settings.";

    public static ReviewResult ReviewCode(string code)
    {
        var hasContent = code.Trim().Length >= 20;
        return new ReviewResult(
            hasContent ? 60 : 20, hasContent ? "partial" : "fail",
            hasContent ? "Saved. The offline tutor cannot run or judge code, so this counts as practice, not proof." : "That looks too short to be a real attempt.",
            Array.Empty<string>(),
            new[] { "Run your code yourself with 2 normal inputs and 1 edge case.", "Set an AI key on the server to get a real review." },
            "", "Check your own code against the rubric, then move on.", false, Source);
    }

    public static ReviewResult ReviewExplanation(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return new ReviewResult(
            words >= 40 ? 60 : 30, words >= 40 ? "partial" : "fail",
            words >= 40 ? "Saved. The offline tutor cannot judge accuracy." : "Try a longer explanation: what it is, one example, one common mistake.",
            Array.Empty<string>(),
            new[] { "Read your answer aloud. Would a colleague understand it?" },
            "", "Compare your answer with the lesson text.", false, Source);
    }

    public static string CoachNote(string summary) =>
        "Offline coach note: " + summary + " Keep tomorrow small and finish the first task before opening anything else.";
}
