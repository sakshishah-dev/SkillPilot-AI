using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Core;

/// <summary>When should a topic come back for revision? Weak results return tomorrow, good ones stretch out.</summary>
public static class SpacedRepetition
{
    public static readonly int[] IntervalsDays = { 1, 3, 7, 14, 30 };

    public static DateOnly? NextDue(IEnumerable<EvidenceItem> evidence)
    {
        var ordered = evidence.OrderBy(e => e.At).ToList();
        if (ordered.Count == 0) return null;

        var last = ordered[^1];
        var lastDay = DateOnly.FromDateTime(last.At);
        var effective = MasteryCalculator.EffectiveLevel(last.Level, last.Assist);

        // A weak or assisted last attempt: try again tomorrow.
        if (last.Score < 60 || effective < EvidenceLevel.SolvedWithHints)
            return lastDay.AddDays(1);

        var goodRuns = ordered.Count(e =>
            e.Score >= 60 && MasteryCalculator.EffectiveLevel(e.Level, e.Assist) >= EvidenceLevel.SolvedWithHints);
        var idx = Math.Clamp(goodRuns - 1, 0, IntervalsDays.Length - 1);
        return lastDay.AddDays(IntervalsDays[idx]);
    }
}
