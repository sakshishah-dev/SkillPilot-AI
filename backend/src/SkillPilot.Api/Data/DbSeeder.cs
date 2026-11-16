using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;

namespace SkillPilot.Api.Data;

public static class DbSeeder
{
    /// <summary>Creates tables if needed and keeps the skill catalog in sync with the code.</summary>
    public static async Task InitAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var skills = await db.Skills.ToDictionaryAsync(s => s.Key);
        foreach (var s in Curriculum.Skills)
        {
            if (skills.TryGetValue(s.Key, out var existing))
            {
                existing.Name = s.Name; existing.Category = s.Category; existing.Order = s.Order;
                existing.Description = s.Description; existing.ResourceName = s.ResourceName; existing.ResourceUrl = s.ResourceUrl;
            }
            else db.Skills.Add(s);
        }

        var topics = await db.Topics.ToDictionaryAsync(t => t.Id);
        foreach (var t in Curriculum.Topics)
        {
            if (topics.TryGetValue(t.Id, out var existing))
            {
                existing.SkillKey = t.SkillKey; existing.Title = t.Title; existing.Order = t.Order;
                existing.Description = t.Description; existing.EstimatedMinutes = t.EstimatedMinutes;
                existing.HasCode = t.HasCode; existing.Language = t.Language;
            }
            else db.Topics.Add(t);
        }
        await db.SaveChangesAsync();
    }
}
