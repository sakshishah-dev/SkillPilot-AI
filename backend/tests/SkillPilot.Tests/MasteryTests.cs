using SkillPilot.Api.Core;
using SkillPilot.Api.Domain;

namespace SkillPilot.Tests;

public class MasteryTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
    private static EvidenceItem E(EvidenceLevel l, AssistMode a = AssistMode.Independent, int score = 100, double daysAgo = 0)
        => new(l, a, score, Now.AddDays(-daysAgo));

    [Fact]
    public void No_evidence_is_zero() => Assert.Equal(0, MasteryCalculator.Compute(Array.Empty<EvidenceItem>(), Now));

    [Fact]
    public void Watching_alone_never_passes_the_watched_ceiling()
    {
        var many = Enumerable.Range(0, 20).Select(_ => E(EvidenceLevel.Watched)).ToList();
        Assert.True(MasteryCalculator.Compute(many, Now) <= 25);
    }

    [Fact]
    public void Understanding_alone_stays_below_mastered()
    {
        var m = MasteryCalculator.Compute(new[] { E(EvidenceLevel.Watched), E(EvidenceLevel.Understood) }, Now);
        Assert.InRange(m, 1, 45);
    }

    [Fact]
    public void Solving_independently_beats_solving_with_hints()
    {
        var alone = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently) }, Now);
        var hinted = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently, AssistMode.Hint) }, Now);
        Assert.True(alone > hinted);
        Assert.True(alone >= MasteryCalculator.MasteredThreshold);
        Assert.True(hinted < MasteryCalculator.MasteredThreshold);
    }

    [Fact]
    public void Viewing_the_solution_only_counts_as_watching()
    {
        var m = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently, AssistMode.Solution) }, Now);
        Assert.True(m <= 25);
    }

    [Fact]
    public void Low_scores_earn_little()
    {
        var m = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently, score: 30) }, Now);
        Assert.True(m < 40);
    }

    [Fact]
    public void Old_evidence_fades()
    {
        var fresh = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently) }, Now);
        var old = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently, daysAgo: 90) }, Now);
        Assert.True(old < fresh);
    }

    [Fact]
    public void Repeating_independent_success_raises_mastery()
    {
        var one = MasteryCalculator.Compute(new[] { E(EvidenceLevel.SolvedIndependently, score: 80) }, Now);
        var three = MasteryCalculator.Compute(Enumerable.Range(0, 3).Select(i => E(EvidenceLevel.SolvedIndependently, score: 80, daysAgo: i)), Now);
        Assert.True(three > one);
        Assert.True(three <= 90);
    }

    [Fact]
    public void Skill_mastery_counts_untouched_topics_as_zero()
        => Assert.Equal(25, MasteryCalculator.SkillMastery(new[] { 100, 0, 0, 0 }));
}

public class SpacedRepetitionTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Weak_attempt_comes_back_tomorrow()
    {
        var due = SpacedRepetition.NextDue(new[] { new EvidenceItem(EvidenceLevel.SolvedIndependently, AssistMode.Independent, 40, Now) });
        Assert.Equal(new DateOnly(2026, 11, 21), due);
    }

    [Fact]
    public void Intervals_stretch_after_repeated_success()
    {
        var one = SpacedRepetition.NextDue(new[] { new EvidenceItem(EvidenceLevel.SolvedIndependently, AssistMode.Independent, 90, Now) });
        var two = SpacedRepetition.NextDue(new[]
        {
            new EvidenceItem(EvidenceLevel.SolvedIndependently, AssistMode.Independent, 90, Now.AddDays(-3)),
            new EvidenceItem(EvidenceLevel.SolvedIndependently, AssistMode.Independent, 90, Now)
        });
        Assert.Equal(new DateOnly(2026, 11, 21), one);
        Assert.Equal(new DateOnly(2026, 11, 23), two);
    }

    [Fact]
    public void No_evidence_means_no_revision() => Assert.Null(SpacedRepetition.NextDue(Array.Empty<EvidenceItem>()));
}
