using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Ai;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;
using SkillPilot.Api.Security;
using SkillPilot.Api.Services;

namespace SkillPilot.Api.Controllers;

[ApiController, Authorize, Route("api")]
public class ReviewController(AppDbContext db, PlanService plans, ReviewService reviews, TutorService tutor) : ControllerBase
{
    [HttpGet("review/week")]
    public async Task<IActionResult> Week([FromQuery] DateOnly? weekStart)
    {
        var uid = User.Id();
        var profile = await plans.ProfileAsync(uid);
        var start = weekStart ?? ReviewService.MondayOf(PlanService.TodayFor(profile));
        return Ok(await reviews.WeekAsync(uid, profile, ReviewService.MondayOf(start)));
    }

    /// <summary>Today's recap, tomorrow's plan, and where help is needed. The AI note is optional and costs one call.</summary>
    [HttpGet("coach/tomorrow")]
    public async Task<IActionResult> Tomorrow([FromQuery] bool ai = false, CancellationToken ct = default)
    {
        var uid = User.Id();
        await plans.EnsureAsync(uid);
        var profile = await plans.ProfileAsync(uid);
        var today = PlanService.TodayFor(profile);
        var tomorrow = today.AddDays(1);

        var todaySessions = await db.Sessions.Where(s => s.UserId == uid && s.Date == today).ToListAsync();
        var todayTasks = await db.StudyTasks.Where(t => t.UserId == uid && t.Date == today).ToListAsync();
        var tomorrowTasks = await db.StudyTasks.Where(t => t.UserId == uid && t.Date == tomorrow && t.Status == StudyTaskStatus.Planned).OrderBy(t => t.Order).ToListAsync();

        // Where is help needed: weak recent results, many hints, or the solution was needed.
        var since = DateTime.UtcNow.AddDays(-10);
        var subs = await db.Submissions.Where(s => s.UserId == uid && s.CreatedAt >= since).ToListAsync();
        var help = subs.GroupBy(s => s.TopicId).Select(g =>
        {
            var latest = g.OrderByDescending(s => s.CreatedAt).First();
            string? reason = latest.Score < 50 ? "Your last attempt scored below 50."
                : latest.Assist == AssistMode.Solution ? "You needed the solution last time, so it has not been proven yet."
                : g.Count(s => s.Score < 60) >= 2 ? "You have struggled here more than once." : null;
            return new { topicId = g.Key, reason };
        }).Where(x => x.reason != null).Take(4)
          .Select(x => new { topic = Dtos.Topic(x.topicId), x.reason }).ToList();

        var doneToday = todayTasks.Count(t => t.Status == StudyTaskStatus.Done);
        var minutes = todaySessions.Sum(s => s.Minutes);
        var notes = string.Join(" | ", todaySessions.Where(s => s.Note.Length > 0).Select(s => s.Note).Take(5));
        var facts = $"Today: studied {minutes} minutes, finished {doneToday} of {todayTasks.Count} planned tasks. " +
                    $"What I did: {(notes.Length > 0 ? notes : "nothing logged")}. " +
                    $"Needs help: {(help.Count > 0 ? string.Join("; ", help.Select(h => ((dynamic)h.topic).title + " (" + h.reason + ")")) : "nothing flagged")}. " +
                    $"Tomorrow starts with: {(tomorrowTasks.Count > 0 ? string.Join(", ", tomorrowTasks.Take(3).Select(t => t.Kind + " " + Curriculum.Find(t.TopicId)!.Title)) : "a free day")}.";

        string? note = null; string source = "rules";
        if (ai) (note, source) = await tutor.CoachNoteAsync(profile, facts, ct);

        return Ok(new
        {
            today = new { minutes, tasksDone = doneToday, tasksTotal = todayTasks.Count, notes = todaySessions.Select(s => s.Note).Where(n => n.Length > 0) },
            tomorrow = new { date = tomorrow, tasks = tomorrowTasks.Select(Dtos.Task) },
            help, summary = facts, note, noteSource = source
        });
    }
}
