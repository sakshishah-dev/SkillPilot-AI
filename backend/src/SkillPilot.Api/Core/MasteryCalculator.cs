using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Core;

public record EvidenceItem(EvidenceLevel Level, AssistMode Assist, int Score, DateTime At);

/// <summary>
/// Turns evidence into a 0..100 mastery number for one topic.
/// Rules: watching is not knowing, AI help lowers credit, old evidence fades,
/// and the level of evidence caps the score (a topic that was only watched can never pass 25).
/// </summary>
public static class MasteryCalculator
{
    public const double HalfLifeDays = 45;
    public const int MasteredThreshold = 70;

    /// <summary>Highest mastery reachable at each effective level.</summary>
    public static int Ceiling(EvidenceLevel level) => level switch
    {
        EvidenceLevel.Watched => 25,
        EvidenceLevel.Understood => 45,
        EvidenceLevel.SolvedWithHints => 65,
        EvidenceLevel.SolvedIndependently => 90,
        EvidenceLevel.AppliedInProject => 100,
        _ => 0
    };

    /// <summary>The level actually earned after AI help is taken into account.</summary>
    public static EvidenceLevel EffectiveLevel(EvidenceLevel level, AssistMode assist) => assist switch
    {
        AssistMode.Solution => EvidenceLevel.Watched,
        AssistMode.Teacher => level > EvidenceLevel.SolvedWithHints ? EvidenceLevel.SolvedWithHints : level,
        AssistMode.Hint => level > EvidenceLevel.SolvedWithHints ? EvidenceLevel.SolvedWithHints : level,
        _ => level
    };

    public static int Compute(IEnumerable<EvidenceItem> evidence, DateTime now)
    {
        double notEarned = 1.0;
        EvidenceLevel top = 0;
        bool any = false;

        foreach (var e in evidence)
        {
            var level = EffectiveLevel(e.Level, e.Assist);
            var quality = Math.Clamp(e.Score, 0, 100) / 100.0;
            var ageDays = Math.Max(0, (now - e.At).TotalDays);
            var decay = Math.Pow(0.5, ageDays / HalfLifeDays);
            var contribution = Ceiling(level) * quality * decay / 100.0;
            if (contribution <= 0) continue;
            any = true;
            if (level > top && quality >= 0.5) top = level;
            notEarned *= 1 - contribution;
        }

        if (!any || top == 0) return 0;
        var mastery = 100 * (1 - notEarned);
        return (int)Math.Round(Math.Min(mastery, Ceiling(top)));
    }

    /// <summary>Average mastery of a skill's topics. Topics with no evidence count as zero.</summary>
    public static int SkillMastery(IEnumerable<int> topicMasteries)
    {
        var list = topicMasteries.ToList();
        return list.Count == 0 ? 0 : (int)Math.Round(list.Average());
    }
}
