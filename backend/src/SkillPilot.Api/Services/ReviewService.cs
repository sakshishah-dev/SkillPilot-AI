using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Services;

public class ReviewService(AppDbContext db, ProgressService progress)
{
    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static int Consistency(int daysStudied, int studyDays, int studied, int available)
    {
        if (studyDays <= 0) return 0;
        var dayPart = Math.Min(1.0, daysStudied / (double)studyDays);
        var timePart = available > 0 ? Math.Min(1.0, studied / (double)available) : 0;
        return (int)Math.Round(100 * (0.6 * dayPart + 0.4 * timePart));
    }

    public async Task<object> WeekAsync(Guid userId, UserProfile profile, DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        var today = PlanService.TodayFor(profile);
        var avail = PlanService.Availability(profile);

        var sessions = await db.Sessions.Where(s => s.UserId == userId && s.Date >= weekStart && s.Date <= weekEnd).ToListAsync();
        var tasks = await db.StudyTasks.Where(t => t.UserId == userId && t.Date >= weekStart && t.Date <= weekEnd).ToListAsync();

        var elapsed = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).Where(d => d <= today).ToList();
        var availableMinutes = elapsed.Sum(d => StudyPlanner.AvailabilityFor(avail, d));
        var studyDays = elapsed.Count(d => StudyPlanner.AvailabilityFor(avail, d) > 0);
        var perDay = sessions.GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));
        var daysStudied = elapsed.Count(d => perDay.GetValueOrDefault(d) >= 10);
        var studied = sessions.Sum(s => s.Minutes);

        var startUtc = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var subs = await db.Submissions.Where(s => s.UserId == userId && s.CreatedAt >= startUtc && s.CreatedAt < endUtc && s.Kind == "code").ToListAsync();
        var independent = subs.Count(s => s.Assist == AssistMode.Independent && s.Score >= 60);
        var assisted = subs.Count(s => s.Assist != AssistMode.Independent && s.Score >= 60);

        var before = await progress.LoadAsync(userId, startUtc, startUtc);
        var now = await progress.LoadAsync(userId);
        var mastered = now.Values
            .Where(p => p.Mastery >= MasteryCalculator.MasteredThreshold && before[p.TopicId].Mastery < MasteryCalculator.MasteredThreshold)
            .Select(p => Curriculum.Find(p.TopicId)!.Title).ToList();

        var revision = now.Values
            .Where(p => (p.RevisionDue.HasValue && p.RevisionDue.Value <= today) || (p.StepsFinished && p.Mastery < MasteryCalculator.MasteredThreshold))
            .Select(p => Curriculum.Find(p.TopicId)!.Title).Take(8).ToList();

        var consistency = Consistency(daysStudied, studyDays, studied, availableMinutes);

        var recs = new List<string>();
        if (studyDays > 0 && daysStudied < studyDays / 2)
            recs.Add("Fewer than half your study days had any study. Use Busy mode on hard days: 20 minutes still counts and keeps the habit alive.");
        if (subs.Count > 0 && independent == 0)
            recs.Add("You solved nothing without help this week. Next practice: open it, write your own attempt first, and only then ask for a hint.");
        if (revision.Count > 0)
            recs.Add($"Revisit: {string.Join(", ", revision.Take(3))}.");
        var skills = await db.UserSkills.Where(s => s.UserId == userId).ToListAsync();
        foreach (var s in skills)
        {
            var topics = Curriculum.Topics.Where(t => t.SkillKey == s.SkillKey).ToList();
            var m = MasteryCalculator.SkillMastery(topics.Select(t => now[t.Id].Mastery));
            if (s.SelfConfidence * 10 - m >= 40)
                recs.Add($"You feel confident in {Curriculum.Skills.First(k => k.Key == s.SkillKey).Name} but your proof is low. An independent exercise there will show the truth.");
        }
        if (recs.Count == 0) recs.Add("Steady week. Keep the same rhythm and finish the first task of each day before anything else.");

        return new
        {
            weekStart, weekEnd,
            availableMinutes, studiedMinutes = studied, daysStudied, studyDays,
            tasksDone = tasks.Count(t => t.Status == StudyTaskStatus.Done),
            tasksMoved = tasks.Count(t => t.Status == StudyTaskStatus.Missed),
            tasksSkipped = tasks.Count(t => t.Status == StudyTaskStatus.Skipped),
            topicsMastered = mastered,
            independentSolves = independent,
            assistedSolves = assisted,
            revisionNeeded = revision,
            consistency,
            minutesByDay = Enumerable.Range(0, 7).Select(i => new { date = weekStart.AddDays(i), minutes = perDay.GetValueOrDefault(weekStart.AddDays(i)), available = StudyPlanner.AvailabilityFor(avail, weekStart.AddDays(i)) }),
            recommendations = recs
        };
    }

    public async Task<int> StreakAsync(Guid userId, DateOnly today)
    {
        var from = today.AddDays(-120);
        var days = (await db.Sessions.Where(s => s.UserId == userId && s.Date >= from).ToListAsync())
            .GroupBy(s => s.Date).Where(g => g.Sum(s => s.Minutes) >= 10).Select(g => g.Key).ToHashSet();
        var d = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(d)) { streak++; d = d.AddDays(-1); }
        return streak;
    }
}
