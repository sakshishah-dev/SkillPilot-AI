using System.Text.Json;
using SkillPilot.Api.Core;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Controllers;

public static class Dtos
{
    public static object Topic(string topicId)
    {
        var t = Curriculum.Find(topicId);
        var s = t == null ? null : Curriculum.Skills.First(x => x.Key == t.SkillKey);
        return new
        {
            id = topicId, title = t?.Title ?? topicId, description = t?.Description ?? "",
            skillKey = t?.SkillKey, skillName = s?.Name, language = t?.Language, hasCode = t?.HasCode ?? false,
            resourceName = s?.ResourceName, resourceUrl = s?.ResourceUrl
        };
    }

    public static object Task(StudyTask t) => new
    {
        id = t.Id, date = t.Date, kind = t.Kind.ToString(), status = t.Status.ToString(),
        minutes = t.PlannedMinutes, why = t.Why, order = t.Order, topic = Topic(t.TopicId)
    };

    public static object Exercise(Exercise e)
    {
        var hints = SafeHints(e);
        return new
        {
            id = e.Id, topicId = e.TopicId, prompt = e.Prompt, starterCode = e.StarterCode, language = e.Language,
            hintsTotal = hints.Length, hintsUsed = e.HintsUsed, hints = hints.Take(e.HintsUsed).ToArray(),
            attempts = e.Attempts, solutionViewed = e.SolutionViewed, canViewSolution = e.Attempts > 0,
            teacherAsks = e.TeacherAsks, source = e.Source,
            assist = Assist(e).ToString()
        };
    }

    public static string[] SafeHints(Exercise e)
    {
        try { return JsonSerializer.Deserialize<string[]>(e.HintsJson) ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    /// <summary>The help level is decided here, from what was actually used. The client cannot claim otherwise.</summary>
    public static AssistMode Assist(Exercise e) =>
        e.SolutionViewed ? AssistMode.Solution
        : e.TeacherAsks > 0 ? AssistMode.Teacher
        : e.HintsUsed > 0 ? AssistMode.Hint
        : AssistMode.Independent;
}
