using SkillPilot.Api.Ai;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Services;

public class LearningService(AppDbContext db, ProgressService progress)
{
    /// <summary>What a code review proves. An unverified review (offline tutor) can never prove more than understanding.</summary>
    public static (EvidenceLevel Level, int Score) CodeEvidence(ReviewResult r)
    {
        if (!r.Verified) return (EvidenceLevel.Understood, 60);
        if (r.Score >= 70) return (EvidenceLevel.SolvedIndependently, r.Score);
        if (r.Score >= 40) return (EvidenceLevel.SolvedWithHints, r.Score);
        return (EvidenceLevel.Understood, r.Score);
    }

    public static (EvidenceLevel Level, int Score) ExplainEvidence(ReviewResult r)
    {
        if (!r.Verified) return (EvidenceLevel.Understood, 60);
        return r.Score >= 40 ? (EvidenceLevel.Understood, r.Score) : (EvidenceLevel.Watched, Math.Max(r.Score, 30));
    }

    public async Task<int> RecordAsync(Guid userId, string topicId, EvidenceLevel level, AssistMode assist, int score, string note)
    {
        var topic = Core.Curriculum.Find(topicId) ?? throw new ArgumentException("Unknown topic.");
        db.Evidence.Add(new Evidence
        {
            UserId = userId, TopicId = topicId, SkillKey = topic.SkillKey, Level = level, Assist = assist,
            Score = Math.Clamp(score, 0, 100), Note = note.Length > 300 ? note[..300] : note
        });
        await db.SaveChangesAsync();
        var p = await progress.LoadAsync(userId);
        return p[topicId].Mastery;
    }

    public static object ReviewDto(ReviewResult r) => new
    {
        r.Score, r.Verdict, r.Summary, r.Issues, r.Suggestions, r.Complexity, r.NextStep, r.Verified, r.Source
    };
}
