using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Services;

public record TopicProgress(
    string TopicId, string SkillKey, int Mastery, HashSet<StudyTaskKind> DoneKinds,
    DateOnly? RevisionDue, int EvidenceCount, bool StepsFinished);

public class ProgressService(AppDbContext db)
{
    public async Task<Dictionary<string, TopicProgress>> LoadAsync(Guid userId, DateTime? now = null, DateTime? evidenceBefore = null)
    {
        var at = now ?? DateTime.UtcNow;
        var evQuery = db.Evidence.Where(e => e.UserId == userId);
        if (evidenceBefore != null) evQuery = evQuery.Where(e => e.CreatedAt < evidenceBefore);
        var evidence = (await evQuery.ToListAsync()).GroupBy(e => e.TopicId).ToDictionary(g => g.Key, g => g.ToList());

        var done = (await db.StudyTasks
                .Where(t => t.UserId == userId && t.Status == StudyTaskStatus.Done && t.Kind != StudyTaskKind.Revision)
                .Select(t => new { t.TopicId, t.Kind }).ToListAsync())
            .GroupBy(t => t.TopicId).ToDictionary(g => g.Key, g => g.Select(x => x.Kind).ToHashSet());

        var result = new Dictionary<string, TopicProgress>();
        foreach (var topic in Curriculum.Topics)
        {
            var items = evidence.TryGetValue(topic.Id, out var list)
                ? list.Select(e => new EvidenceItem(e.Level, e.Assist, e.Score, e.CreatedAt)).ToList()
                : new List<EvidenceItem>();
            var mastery = MasteryCalculator.Compute(items, at);
            var kinds = done.TryGetValue(topic.Id, out var k) ? k : new HashSet<StudyTaskKind>();
            var required = RequiredKinds(topic);
            var finished = required.All(kinds.Contains);
            // Spaced revision only for topics that are known; weak finished topics get a Revision step instead.
            var due = mastery >= MasteryCalculator.MasteredThreshold ? SpacedRepetition.NextDue(items) : null;
            result[topic.Id] = new TopicProgress(topic.Id, topic.SkillKey, mastery, kinds, due, items.Count, finished);
        }
        return result;
    }

    public static StudyTaskKind[] RequiredKinds(Topic t) =>
        t.HasCode
            ? new[] { StudyTaskKind.Learn, StudyTaskKind.Practice, StudyTaskKind.Explain }
            : new[] { StudyTaskKind.Learn, StudyTaskKind.Explain };

    public static Dictionary<string, TopicState> ToStates(Dictionary<string, TopicProgress> progress) =>
        progress.ToDictionary(p => p.Key, p => new TopicState(p.Key, p.Value.Mastery, p.Value.DoneKinds, p.Value.RevisionDue));

    public static string Status(TopicProgress p) =>
        p.Mastery >= MasteryCalculator.MasteredThreshold ? "mastered"
        : p.EvidenceCount > 0 || p.DoneKinds.Count > 0 ? "learning" : "not-started";

    /// <summary>A compact summary for the AI. Never the whole history.</summary>
    public async Task<string> LearnerContextAsync(Guid userId)
    {
        var progress = await LoadAsync(userId);
        string Name(string id) => Curriculum.Find(id)?.Title ?? id;
        var strong = progress.Values.Where(p => p.Mastery >= 70).OrderByDescending(p => p.Mastery).Take(5).Select(p => Name(p.TopicId));
        var weak = progress.Values.Where(p => p.EvidenceCount > 0 && p.Mastery < 50).OrderBy(p => p.Mastery).Take(5).Select(p => Name(p.TopicId));
        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7);
        var minutes = await db.Sessions.Where(s => s.UserId == userId && s.Date >= since).SumAsync(s => (int?)s.Minutes) ?? 0;
        return $"Early career developer with job experience in .NET and Angular. Independent coding is weak. " +
               $"Strong: {(strong.Any() ? string.Join(", ", strong) : "nothing proven yet")}. " +
               $"Struggling: {(weak.Any() ? string.Join(", ", weak) : "none recorded")}. Studied {minutes} minutes in the last 7 days.";
    }
}
