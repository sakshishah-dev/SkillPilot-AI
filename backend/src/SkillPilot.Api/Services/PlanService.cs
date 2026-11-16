using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Services;

public class PlanService(AppDbContext db, ProgressService progress)
{
    public static DateOnly TodayFor(UserProfile profile, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(profile.Timezone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { tz = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, tz));
    }

    public static int[] Availability(UserProfile p)
    {
        int[] a;
        try { a = JsonSerializer.Deserialize<int[]>(p.AvailabilityJson) ?? new int[7]; } catch (JsonException) { a = new int[7]; }
        return Enumerable.Range(0, 7).Select(i => i < a.Length ? Math.Clamp(a[i], 0, 600) : 0).ToArray();
    }

    public static string[] Priority(UserProfile p)
    {
        string[] pr;
        try { pr = JsonSerializer.Deserialize<string[]>(p.PriorityJson) ?? Array.Empty<string>(); } catch (JsonException) { pr = Array.Empty<string>(); }
        var valid = pr.Where(k => Curriculum.Skills.Any(s => s.Key == k)).Distinct().ToList();
        foreach (var k in Curriculum.DefaultPriority) if (!valid.Contains(k)) valid.Add(k);
        return valid.ToArray();
    }

    public async Task<UserProfile> ProfileAsync(Guid userId)
    {
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (p == null)
        {
            p = new UserProfile { UserId = userId, PriorityJson = JsonSerializer.Serialize(Curriculum.DefaultPriority) };
            db.Profiles.Add(p);
            await db.SaveChangesAsync();
        }
        return p;
    }

    public async Task<DayMode> ModeAsync(Guid userId, DateOnly date) =>
        (await db.DayStates.FirstOrDefaultAsync(d => d.UserId == userId && d.Date == date))?.Mode ?? DayMode.Normal;

    /// <summary>Called whenever the plan is opened: marks lost days, rolls the window forward, never piles up a backlog.</summary>
    public async Task EnsureAsync(Guid userId)
    {
        var profile = await ProfileAsync(userId);
        var today = TodayFor(profile);

        var overdue = await db.StudyTasks
            .Where(t => t.UserId == userId && t.Status == StudyTaskStatus.Planned && t.Date < today).ToListAsync();
        foreach (var t in overdue) t.Status = StudyTaskStatus.Missed;
        if (overdue.Count > 0) await db.SaveChangesAsync();

        var future = await db.StudyTasks
            .Where(t => t.UserId == userId && t.Status == StudyTaskStatus.Planned && t.Date >= today)
            .Select(t => t.Date).ToListAsync();

        var hasTime = Availability(profile).Any(a => a > 0);
        var needs = overdue.Count > 0 || (future.Count == 0 && hasTime) || (future.Count > 0 && future.Max() < today.AddDays(2));
        if (!needs) return;

        var ever = await db.PlanChanges.AnyAsync(c => c.UserId == userId);
        var reason = overdue.Count > 0
            ? $"{overdue.Count} planned task(s) from earlier days were not done. Nothing is lost: each topic continues from its next step, rebuilt for the days ahead within your normal time."
            : !ever ? "First plan built from your profile and availability."
            : "Plan rolled forward so you always see the next 7 days.";
        await ReplanAsync(userId, reason);
    }

    /// <summary>Rebuilds all future planned tasks. A reason is stored in the audit trail when given.</summary>
    public async Task<int> ReplanAsync(Guid userId, string? reason)
    {
        var profile = await ProfileAsync(userId);
        var today = TodayFor(profile);
        var mode = await ModeAsync(userId, today);

        var sessionMinutes = await db.Sessions.Where(s => s.UserId == userId && s.Date == today).SumAsync(s => (int?)s.Minutes) ?? 0;
        var doneTaskMinutes = await db.StudyTasks
            .Where(t => t.UserId == userId && t.Date == today && t.Status == StudyTaskStatus.Done).SumAsync(t => (int?)t.PlannedMinutes) ?? 0;

        var states = ProgressService.ToStates(await progress.LoadAsync(userId));

        var stale = await db.StudyTasks
            .Where(t => t.UserId == userId && t.Status == StudyTaskStatus.Planned && t.Date >= today).ToListAsync();
        db.StudyTasks.RemoveRange(stale);

        var input = new PlanInput(today, Availability(profile), mode, Math.Max(sessionMinutes, doneTaskMinutes),
            Priority(profile), states);
        var items = StudyPlanner.PlanWeek(input);
        foreach (var i in items)
            db.StudyTasks.Add(new StudyTask
            {
                UserId = userId, Date = i.Date, TopicId = i.TopicId, Kind = i.Kind,
                PlannedMinutes = i.Minutes, Why = i.Why, Order = i.Order
            });

        if (reason != null && items.Count > 0)
            db.PlanChanges.Add(new PlanChange
            {
                UserId = userId, Reason = reason,
                Summary = $"{items.Count} task(s), {items.Sum(i => i.Minutes)} minutes over {items.Select(i => i.Date).Distinct().Count()} day(s)."
            });
        await db.SaveChangesAsync();
        return items.Count;
    }

    public async Task SetModeAsync(Guid userId, DayMode mode)
    {
        var profile = await ProfileAsync(userId);
        var today = TodayFor(profile);
        var state = await db.DayStates.FirstOrDefaultAsync(d => d.UserId == userId && d.Date == today);
        if (state == null) db.DayStates.Add(new DayState { UserId = userId, Date = today, Mode = mode });
        else state.Mode = mode;
        await db.SaveChangesAsync();
        var label = mode switch
        {
            DayMode.Busy => "a busy day (20 minutes)",
            DayMode.Minimal => "a minimal day (10 minutes)",
            DayMode.Off => "a day off",
            _ => "a normal day"
        };
        await ReplanAsync(userId, $"You marked today as {label}. Tomorrow is unaffected and nothing was added to it.");
    }
}
