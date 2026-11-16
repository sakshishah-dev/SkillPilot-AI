using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;
using SkillPilot.Api.Security;
using SkillPilot.Api.Services;

namespace SkillPilot.Api.Controllers;

public record SessionRequest([Range(1, 600)] int Minutes, [MaxLength(1000)] string? Note, [Range(1, 3)] int Energy = 2, DateOnly? Date = null, Guid? TaskId = null);

[ApiController, Authorize, Route("api/sessions")]
public class SessionsController(AppDbContext db, PlanService plans) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Log(SessionRequest req)
    {
        var uid = User.Id();
        var profile = await plans.ProfileAsync(uid);
        var today = PlanService.TodayFor(profile);
        var date = req.Date ?? today;
        if (date > today || date < today.AddDays(-30)) return BadRequest(new { error = "Choose a date from the last 30 days." });
        if (req.TaskId != null && !await db.StudyTasks.AnyAsync(t => t.Id == req.TaskId && t.UserId == uid))
            return BadRequest(new { error = "Unknown task." });
        var s = new StudySession { UserId = uid, Date = date, Minutes = req.Minutes, Note = (req.Note ?? "").Trim(), Energy = req.Energy, TaskId = req.TaskId };
        db.Sessions.Add(s);
        await db.SaveChangesAsync();
        return Ok(new { s.Id, s.Date, s.Minutes, s.Note, s.Energy });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int days = 14)
    {
        var uid = User.Id();
        var profile = await plans.ProfileAsync(uid);
        var today = PlanService.TodayFor(profile);
        var from = today.AddDays(-Math.Clamp(days, 1, 90) + 1);
        var rows = await db.Sessions.Where(s => s.UserId == uid && s.Date >= from).OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).ToListAsync();
        var perDay = Enumerable.Range(0, (today.DayNumber - from.DayNumber) + 1).Select(i => from.AddDays(i))
            .Select(d => new { date = d, minutes = rows.Where(r => r.Date == d).Sum(r => r.Minutes) }).Reverse();
        return Ok(new { perDay, sessions = rows.Select(s => new { s.Id, s.Date, s.Minutes, s.Note, s.Energy, s.TaskId }) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = User.Id();
        var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (s == null) return NotFound();
        db.Sessions.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
