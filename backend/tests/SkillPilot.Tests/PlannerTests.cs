using SkillPilot.Api.Core;
using SkillPilot.Api.Domain;

namespace SkillPilot.Tests;

public class PlannerTests
{
    // Friday 20 Nov 2026
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly int[] Normal = { 90, 90, 90, 90, 90, 180, 180 };

    private static PlanInput Input(DayMode mode = DayMode.Normal, int doneToday = 0,
        IReadOnlyDictionary<string, TopicState>? states = null, string[]? priority = null, int[]? avail = null)
        => new(Today, avail ?? Normal, mode, doneToday, priority ?? Curriculum.DefaultPriority,
               states ?? new Dictionary<string, TopicState>());

    [Fact]
    public void Never_plans_more_than_the_available_time()
    {
        var plan = StudyPlanner.PlanWeek(Input());
        foreach (var day in plan.GroupBy(p => p.Date))
        {
            var budget = StudyPlanner.AvailabilityFor(Normal, day.Key);
            Assert.True(day.Sum(p => p.Minutes) <= budget, $"{day.Key} over budget");
        }
    }

    [Fact]
    public void Plans_every_day_that_has_time()
    {
        var plan = StudyPlanner.PlanWeek(Input());
        Assert.Equal(7, plan.Select(p => p.Date).Distinct().Count());
    }

    [Fact]
    public void Busy_day_is_capped_at_20_minutes_and_minimal_at_10()
    {
        Assert.True(StudyPlanner.PlanWeek(Input(DayMode.Busy)).Where(p => p.Date == Today).Sum(p => p.Minutes) <= 20);
        Assert.True(StudyPlanner.PlanWeek(Input(DayMode.Minimal)).Where(p => p.Date == Today).Sum(p => p.Minutes) <= 10);
    }

    [Fact]
    public void Off_day_plans_nothing_today_but_tomorrow_is_normal()
    {
        var plan = StudyPlanner.PlanWeek(Input(DayMode.Off));
        Assert.DoesNotContain(plan, p => p.Date == Today);
        Assert.Contains(plan, p => p.Date == Today.AddDays(1));
    }

    [Fact]
    public void Time_already_studied_today_is_subtracted()
    {
        var plan = StudyPlanner.PlanWeek(Input(doneToday: 80));
        Assert.True(plan.Where(p => p.Date == Today).Sum(p => p.Minutes) <= 10);
    }

    [Fact]
    public void Starts_with_a_learn_step_then_practice_for_a_new_topic()
    {
        var plan = StudyPlanner.PlanWeek(Input(priority: new[] { "csharp" }, avail: new[] { 60, 60, 60, 60, 60, 60, 60 }));
        var first = plan.Where(p => p.TopicId == "csharp.types").OrderBy(p => p.Date).ThenBy(p => p.Order).Select(p => p.Kind).ToList();
        Assert.Equal(StudyTaskKind.Learn, first[0]);
        Assert.Equal(StudyTaskKind.Practice, first[1]);
    }

    [Fact]
    public void Does_not_restart_topics_that_are_already_mastered()
    {
        var states = new Dictionary<string, TopicState>
        {
            ["dsa.complexity"] = new("dsa.complexity", 80, new HashSet<StudyTaskKind> { StudyTaskKind.Learn, StudyTaskKind.Explain }, null),
            ["dsa.arrays"] = new("dsa.arrays", 85, new HashSet<StudyTaskKind> { StudyTaskKind.Learn, StudyTaskKind.Practice, StudyTaskKind.Explain }, null),
        };
        var plan = StudyPlanner.PlanWeek(Input(states: states, priority: new[] { "dsa" }));
        Assert.DoesNotContain(plan, p => p.TopicId == "dsa.arrays");
        Assert.DoesNotContain(plan, p => p.TopicId == "dsa.complexity");
        Assert.Contains(plan, p => p.TopicId == "dsa.strings");
    }

    [Fact]
    public void Continues_from_the_step_after_the_last_completed_one()
    {
        var states = new Dictionary<string, TopicState>
        {
            ["csharp.types"] = new("csharp.types", 20, new HashSet<StudyTaskKind> { StudyTaskKind.Learn }, null),
        };
        var plan = StudyPlanner.PlanWeek(Input(states: states, priority: new[] { "csharp" }));
        var firstItem = plan.OrderBy(p => p.Date).ThenBy(p => p.Order).First();
        Assert.Equal("csharp.types", firstItem.TopicId);
        Assert.Equal(StudyTaskKind.Practice, firstItem.Kind);
    }

    [Fact]
    public void Weak_topic_with_all_steps_done_gets_a_revision_step_not_a_restart()
    {
        var all = new HashSet<StudyTaskKind> { StudyTaskKind.Learn, StudyTaskKind.Practice, StudyTaskKind.Explain };
        var states = new Dictionary<string, TopicState>
        {
            ["csharp.types"] = new("csharp.types", 30, all, null),
        };
        var plan = StudyPlanner.PlanWeek(Input(states: states, priority: new[] { "csharp" }));
        Assert.Contains(plan, p => p.TopicId == "csharp.types" && p.Kind == StudyTaskKind.Revision);
        Assert.DoesNotContain(plan, p => p.TopicId == "csharp.types" && p.Kind == StudyTaskKind.Learn);
        Assert.Equal(1, plan.Count(p => p.TopicId == "csharp.types" && p.Kind == StudyTaskKind.Revision));
    }

    [Fact]
    public void Due_spaced_revision_is_scheduled_once_and_is_small()
    {
        var states = new Dictionary<string, TopicState>
        {
            ["csharp.types"] = new("csharp.types", 85, new HashSet<StudyTaskKind> { StudyTaskKind.Learn, StudyTaskKind.Practice, StudyTaskKind.Explain }, Today),
        };
        var plan = StudyPlanner.PlanWeek(Input(states: states));
        var rev = plan.Where(p => p.TopicId == "csharp.types" && p.Kind == StudyTaskKind.Revision).ToList();
        Assert.Single(rev);
        Assert.Equal(Today, rev[0].Date);
        Assert.True(rev[0].Minutes <= 35);
    }

    [Fact]
    public void Main_skill_and_dsa_both_appear_in_the_week()
    {
        var plan = StudyPlanner.PlanWeek(Input(priority: new[] { "csharp", "dsa" }));
        Assert.Contains(plan, p => p.TopicId.StartsWith("csharp."));
        Assert.Contains(plan, p => p.TopicId.StartsWith("dsa."));
    }

    [Fact]
    public void Missing_days_do_not_create_a_backlog()
    {
        // Same state, planned again after two lost days: still never above the daily budget.
        var plan = StudyPlanner.PlanWeek(Input());
        var again = StudyPlanner.PlanWeek(Input() with { Today = Today.AddDays(2) });
        foreach (var day in again.GroupBy(p => p.Date))
            Assert.True(day.Sum(p => p.Minutes) <= StudyPlanner.AvailabilityFor(Normal, day.Key));
        Assert.True(plan.Count > 0 && again.Count > 0);
    }
}
