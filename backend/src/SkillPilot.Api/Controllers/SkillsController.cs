using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Security;
using SkillPilot.Api.Services;

namespace SkillPilot.Api.Controllers;

[ApiController, Authorize, Route("api/skills")]
public class SkillsController(AppDbContext db, ProgressService progress) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var uid = User.Id();
        var prog = await progress.LoadAsync(uid);
        var conf = await db.UserSkills.Where(s => s.UserId == uid).ToDictionaryAsync(s => s.SkillKey, s => s.SelfConfidence);

        var skills = Curriculum.Skills.OrderBy(s => s.Order).Select(s =>
        {
            var topics = Curriculum.Topics.Where(t => t.SkillKey == s.Key).OrderBy(t => t.Order).ToList();
            var mastery = MasteryCalculator.SkillMastery(topics.Select(t => prog[t.Id].Mastery));
            var confidence = conf.GetValueOrDefault(s.Key, 0);
            return new
            {
                key = s.Key, name = s.Name, category = s.Category, description = s.Description,
                resourceName = s.ResourceName, resourceUrl = s.ResourceUrl,
                mastery, confidence,
                // positive: you feel more sure than your evidence shows
                gap = confidence * 10 - mastery,
                topicsMastered = topics.Count(t => prog[t.Id].Mastery >= MasteryCalculator.MasteredThreshold),
                topicCount = topics.Count,
                topics = topics.Select(t => new
                {
                    id = t.Id, title = t.Title, mastery = prog[t.Id].Mastery, status = ProgressService.Status(prog[t.Id]),
                    done = prog[t.Id].DoneKinds.Select(k => k.ToString()), revisionDue = prog[t.Id].RevisionDue
                })
            };
        });
        return Ok(skills);
    }
}
