using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Core;

public record TopicState(string TopicId, int Mastery, HashSet<StudyTaskKind> DoneKinds, DateOnly? RevisionDue);

public record PlanInput(
    DateOnly Today,
    int[] Availability,                       // minutes, Monday..Sunday
    DayMode TodayMode,
    int DoneMinutesToday,                     // minutes already studied or planned-and-done today
    IReadOnlyList<string> Priority,           // skill keys, most important first
    IReadOnlyDictionary<string, TopicState> States,
    int Days = 7);

public record PlannedItem(DateOnly Date, string TopicId, StudyTaskKind Kind, int Minutes, string Why, int Order);

/// <summary>
/// Rule based planner. It never schedules more than the time you have, never stacks a backlog
/// (unfinished work simply stays next in the queue), and carries on from real progress.
/// Two lanes run side by side: your main skill (60 percent) and DSA (40 percent).
/// </summary>
public static class StudyPlanner
{
    private const int MinSlot = 10;
    private const double MainShare = 0.6;

    public static int AvailabilityFor(int[] availability, DateOnly date)
    {
        var idx = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return idx < availability.Length ? Math.Max(0, availability[idx]) : 0;
    }

    public static int DayBudget(PlanInput input, int dayOffset)
    {
        var date = input.Today.AddDays(dayOffset);
        var budget = AvailabilityFor(input.Availability, date);
        if (dayOffset == 0)
        {
            budget = input.TodayMode switch
            {
                DayMode.Busy => Math.Min(budget, 20),
                DayMode.Minimal => Math.Min(budget, 10),
                DayMode.Off => 0,
                _ => budget
            };
            budget = Math.Max(0, budget - input.DoneMinutesToday);
        }
        return budget;
    }

    private record Step(string TopicId, StudyTaskKind Kind, int Minutes, string Why);

    private static int Round5(double v) => (int)(Math.Round(v / 5.0) * 5);

    private static List<Step> StepsFor(Topic topic, TopicState state, HashSet<StudyTaskKind> virtualDone)
    {
        var steps = new List<Step>();
        var est = topic.EstimatedMinutes;
        void Add(StudyTaskKind kind, int minutes, string why)
        {
            if (!state.DoneKinds.Contains(kind) && !virtualDone.Contains(kind))
                steps.Add(new Step(topic.Id, kind, minutes, why));
        }

        Add(StudyTaskKind.Learn, Math.Max(15, Round5(est * 0.30)), $"Start here: {topic.Description}");
        if (topic.HasCode)
            Add(StudyTaskKind.Practice, Math.Max(20, Round5(est * 0.45)),
                "Write it yourself. Reading is not enough to prove you can do it.");
        Add(StudyTaskKind.Explain, Math.Max(10, Round5(est * 0.25)),
            "Explain it in your own words. If you cannot explain it, you do not own it yet.");

        // Everything done but the evidence is still weak: one more fresh attempt.
        var allDone = !steps.Any();
        if (allDone && state.Mastery < MasteryCalculator.MasteredThreshold && !virtualDone.Contains(StudyTaskKind.Revision))
            steps.Add(new Step(topic.Id, StudyTaskKind.Revision, 20,
                "Your evidence for this topic is still weak. Try a fresh problem."));
        return steps;
    }

    public static List<PlannedItem> PlanWeek(PlanInput input)
    {
        var items = new List<PlannedItem>();
        var topicsBySkill = Curriculum.Topics.GroupBy(t => t.SkillKey).ToDictionary(g => g.Key, g => g.OrderBy(t => t.Order).ToList());
        var virtualDone = Curriculum.Topics.ToDictionary(t => t.Id, _ => new HashSet<StudyTaskKind>());
        var placedRevision = new HashSet<string>();

        TopicState StateOf(string id) =>
            input.States.TryGetValue(id, out var s) ? s : new TopicState(id, 0, new HashSet<StudyTaskKind>(), null);

        bool TopicOpen(Topic t)
        {
            var st = StateOf(t.Id);
            if (st.Mastery >= MasteryCalculator.MasteredThreshold) return false;
            return StepsFor(t, st, virtualDone[t.Id]).Any();
        }

        bool SkillOpen(string key) => topicsBySkill.TryGetValue(key, out var ts) && ts.Any(TopicOpen);

        Step? NextStepInSkill(string skillKey)
        {
            if (!topicsBySkill.TryGetValue(skillKey, out var ts)) return null;
            foreach (var t in ts)
            {
                if (!TopicOpen(t)) continue;
                return StepsFor(t, StateOf(t.Id), virtualDone[t.Id]).First();
            }
            return null;
        }

        string? PickSkill(Func<string, bool> accept) =>
            input.Priority.Concat(Curriculum.Skills.Select(s => s.Key)).Distinct()
                .FirstOrDefault(k => accept(k) && SkillOpen(k));

        var assigned = new[] { 0, 0 }; // main lane, dsa lane

        for (int d = 0; d < input.Days; d++)
        {
            var date = input.Today.AddDays(d);
            var remaining = DayBudget(input, d);
            if (remaining < MinSlot) continue;
            int order = 0;

            // 1) A spaced revision that has come due (at most one a day, at most 35 percent of the day)
            var due = input.States.Values
                .Where(s => s.RevisionDue.HasValue && s.RevisionDue.Value <= date && !placedRevision.Contains(s.TopicId))
                .OrderBy(s => s.RevisionDue)
                .FirstOrDefault();
            var minimalToday = d == 0 && input.TodayMode == DayMode.Minimal;
            if (due != null)
            {
                var topic = Curriculum.Find(due.TopicId);
                var cap = minimalToday ? remaining : Math.Min(15, (int)(remaining * 0.35) / 5 * 5);
                var mins = Math.Min(remaining, Math.Max(MinSlot, cap));
                if (topic != null && mins >= MinSlot)
                {
                    var overdue = date.DayNumber - due.RevisionDue!.Value.DayNumber;
                    items.Add(new PlannedItem(date, topic.Id, StudyTaskKind.Revision, mins,
                        overdue > 0 ? $"Spaced revision, due {overdue} day(s) ago. Quick recall keeps it from fading."
                                    : "Spaced revision, due today. Quick recall keeps it from fading.", order++));
                    placedRevision.Add(topic.Id);
                    virtualDone[topic.Id].Add(StudyTaskKind.Revision);
                    remaining -= mins;
                }
            }

            // 2) Learning work, split between two lanes
            while (remaining >= MinSlot)
            {
                var dsaOpen = SkillOpen("dsa");
                string? main = input.Priority.FirstOrDefault() == "dsa"
                    ? PickSkill(k => k == "dsa")
                    : PickSkill(k => k != "dsa");
                string? side = input.Priority.FirstOrDefault() == "dsa"
                    ? PickSkill(k => k != "dsa")
                    : (dsaOpen ? "dsa" : null);

                var candidates = new List<(string skill, int lane)>();
                if (main != null) candidates.Add((main, 0));
                if (side != null) candidates.Add((side, 1));
                if (candidates.Count == 0) break;

                // lane furthest below its share goes first
                var chosen = candidates.Count == 1
                    ? candidates[0]
                    : candidates.OrderBy(c => assigned[c.lane] / (c.lane == 0 ? MainShare : 1 - MainShare)).First();

                var step = NextStepInSkill(chosen.skill);
                if (step == null) break;

                var minutes = step.Minutes;
                var why = step.Why;
                if (minutes > remaining)
                {
                    var shrunk = remaining / 5 * 5;
                    if (shrunk >= MinSlot && (order == 0 || shrunk >= minutes * 0.6))
                    {
                        minutes = shrunk;
                        why += " (short version for today)";
                    }
                    else break;
                }

                items.Add(new PlannedItem(date, step.TopicId, step.Kind, minutes, why, order++));
                virtualDone[step.TopicId].Add(step.Kind);
                assigned[chosen.lane] += minutes;
                remaining -= minutes;
            }
        }

        return items;
    }
}
