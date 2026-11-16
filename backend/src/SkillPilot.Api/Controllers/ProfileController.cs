using System.Text.Json;
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

public record ProfileUpdate(string? TargetRole, DateOnly? TargetDate, int[]? Availability, string[]? Priority,
    string? AiProvider, string? Timezone, bool? ReminderEnabled, int? ReminderIntervalMinutes, bool? OnboardingDone);
public record ConfidenceItem(string SkillKey, int Confidence);

[ApiController, Authorize, Route("api/profile")]
public class ProfileController(AppDbContext db, PlanService plans, TutorService tutor) : ControllerBase
{
    private static readonly string[] Providers = { "auto", "anthropic", "openai", "offline" };

    private async Task<object> BuildAsync(Guid uid)
    {
        var p = await plans.ProfileAsync(uid);
        var conf = await db.UserSkills.Where(s => s.UserId == uid).ToDictionaryAsync(s => s.SkillKey, s => s.SelfConfidence);
        return new
        {
            p.TargetRole, p.TargetDate, availability = PlanService.Availability(p), priority = PlanService.Priority(p),
            p.AiProvider, p.Timezone, p.ReminderEnabled, p.ReminderIntervalMinutes, p.OnboardingDone,
            confidence = Curriculum.Skills.Select(s => new { skillKey = s.Key, confidence = conf.GetValueOrDefault(s.Key, 0) }),
            ai = tutor.ProviderStatus(p)
        };
    }

    [HttpGet] public async Task<IActionResult> Get() => Ok(await BuildAsync(User.Id()));

    [HttpPut]
    public async Task<IActionResult> Update(ProfileUpdate req)
    {
        var uid = User.Id();
        var p = await plans.ProfileAsync(uid);
        var replan = false;

        if (req.TargetRole != null) p.TargetRole = req.TargetRole.Trim()[..Math.Min(req.TargetRole.Trim().Length, 120)];
        if (req.TargetDate.HasValue) p.TargetDate = req.TargetDate;
        if (req.Availability != null)
        {
            if (req.Availability.Length != 7 || req.Availability.Any(a => a < 0 || a > 600))
                return BadRequest(new { error = "Availability needs 7 values between 0 and 600 minutes." });
            p.AvailabilityJson = JsonSerializer.Serialize(req.Availability); replan = true;
        }
        if (req.Priority != null)
        {
            if (req.Priority.Any(k => Curriculum.Skills.All(s => s.Key != k)))
                return BadRequest(new { error = "Unknown skill in priority list." });
            p.PriorityJson = JsonSerializer.Serialize(req.Priority); replan = true;
        }
        if (req.AiProvider != null)
        {
            if (!Providers.Contains(req.AiProvider)) return BadRequest(new { error = "Unknown AI provider." });
            p.AiProvider = req.AiProvider;
        }
        if (req.Timezone != null)
        {
            try { TimeZoneInfo.FindSystemTimeZoneById(req.Timezone); p.Timezone = req.Timezone; }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return BadRequest(new { error = "Unknown timezone." }); }
        }
        if (req.ReminderEnabled.HasValue) p.ReminderEnabled = req.ReminderEnabled.Value;
        if (req.ReminderIntervalMinutes.HasValue) p.ReminderIntervalMinutes = Math.Clamp(req.ReminderIntervalMinutes.Value, 0, 120);
        if (req.OnboardingDone.HasValue) p.OnboardingDone = req.OnboardingDone.Value;
        await db.SaveChangesAsync();

        if (replan) await plans.ReplanAsync(uid, "You changed your study time or skill order, so the plan was rebuilt from your actual progress.");
        return Ok(await BuildAsync(uid));
    }

    [HttpPut("confidence")]
    public async Task<IActionResult> Confidence(ConfidenceItem[] items)
    {
        var uid = User.Id();
        var existing = await db.UserSkills.Where(s => s.UserId == uid).ToDictionaryAsync(s => s.SkillKey);
        foreach (var i in items)
        {
            if (Curriculum.Skills.All(s => s.Key != i.SkillKey)) return BadRequest(new { error = "Unknown skill." });
            var value = Math.Clamp(i.Confidence, 0, 10);
            if (existing.TryGetValue(i.SkillKey, out var row)) row.SelfConfidence = value;
            else db.UserSkills.Add(new UserSkill { UserId = uid, SkillKey = i.SkillKey, SelfConfidence = value });
        }
        await db.SaveChangesAsync();
        return Ok(await BuildAsync(uid));
    }
}
