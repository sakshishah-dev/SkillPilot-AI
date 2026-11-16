using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;
using SkillPilot.Api.Security;
using SkillPilot.Api.Services;

namespace SkillPilot.Api.Controllers;

public record DayModeRequest(DayMode Mode);

[ApiController, Authorize, Route("api/plan")]
public class PlanController(AppDbContext db, PlanService plans, ReviewService reviews) : ControllerBase
{
    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var uid = User.Id();
        await plans.EnsureAsync(uid);
        var profile = await plans.ProfileAsync(uid);
        var today = PlanService.TodayFor(profile);
        var mode = await plans.ModeAsync(uid, today);
        var tasks = await db.StudyTasks.Where(t => t.UserId == uid && t.Date == today).OrderBy(t => t.Order).ToListAsync();
        var studied = await db.Sessions.Where(s => s.UserId == uid && s.Date == today).SumAsync(s => (int?)s.Minutes) ?? 0;
        var moved = await db.StudyTasks.CountAsync(t => t.UserId == uid && t.Status == StudyTaskStatus.Missed && t.Date >= today.AddDays(-7));
        var change = await db.PlanChanges.Where(c => c.UserId == uid).OrderByDescending(c => c.At).FirstOrDefaultAsync();
        var normal = StudyPlanner.AvailabilityFor(PlanService.Availability(profile), today);

        return Ok(new
        {
            date = today, mode = mode.ToString(), availableMinutes = normal,
            studiedMinutes = studied,
            plannedMinutes = tasks.Where(t => t.Status != StudyTaskStatus.Skipped).Sum(t => t.PlannedMinutes),
            doneMinutes = tasks.Where(t => t.Status == StudyTaskStatus.Done).Sum(t => t.PlannedMinutes),
            streak = await reviews.StreakAsync(uid, today),
            movedRecently = moved,
            lastChange = change == null || change.At < DateTime.UtcNow.AddHours(-36) ? null : new { change.Reason, change.Summary, change.At },
            tasks = tasks.Select(Dtos.Task)
        });
    }

    [HttpGet("week")]
    public async Task<IActionResult> Week()
    {
        var uid = User.Id();
        await plans.EnsureAsync(uid);
        var profile = await plans.ProfileAsync(uid);
        var today = PlanService.TodayFor(profile);
        var avail = PlanService.Availability(profile);
        var tasks = await db.StudyTasks.Where(t => t.UserId == uid && t.Date >= today && t.Date < today.AddDays(7)).OrderBy(t => t.Date).ThenBy(t => t.Order).ToListAsync();
        var days = Enumerable.Range(0, 7).Select(i =>
        {
            var d = today.AddDays(i);
            return new
            {
                date = d, availableMinutes = StudyPlanner.AvailabilityFor(avail, d),
                tasks = tasks.Where(t => t.Date == d).Select(Dtos.Task)
            };
        });
        return Ok(new { days });
    }

    [HttpGet("changes")]
    public async Task<IActionResult> Changes() =>
        Ok(await db.PlanChanges.Where(c => c.UserId == User.Id()).OrderByDescending(c => c.At).Take(20)
            .Select(c => new { c.At, c.Reason, c.Summary }).ToListAsync());

    [HttpPost("day-mode")]
    public async Task<IActionResult> DayMode(DayModeRequest req)
    {
        await plans.SetModeAsync(User.Id(), req.Mode);
        return await Today();
    }

    [HttpPost("replan")]
    public async Task<IActionResult> Replan()
    {
        await plans.ReplanAsync(User.Id(), "You asked for the plan to be rebuilt.");
        return await Today();
    }
}
