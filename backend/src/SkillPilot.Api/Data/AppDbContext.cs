using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Domain;

namespace SkillPilot.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<StudyTask> StudyTasks => Set<StudyTask>();
    public DbSet<StudySession> Sessions => Set<StudySession>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<PlanChange> PlanChanges => Set<PlanChange>();
    public DbSet<DayState> DayStates => Set<DayState>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().HasOne(u => u.Profile).WithOne().HasForeignKey<UserProfile>(p => p.UserId);
        b.Entity<UserProfile>().HasKey(p => p.UserId);
        b.Entity<Skill>().HasKey(s => s.Key);
        b.Entity<Topic>().HasKey(t => t.Id);
        b.Entity<UserSkill>().HasKey(s => new { s.UserId, s.SkillKey });
        b.Entity<DayState>().HasKey(d => new { d.UserId, d.Date });

        b.Entity<Evidence>().Property(e => e.Level).HasConversion<string>();
        b.Entity<Evidence>().Property(e => e.Assist).HasConversion<string>();
        b.Entity<Evidence>().HasIndex(e => new { e.UserId, e.TopicId });
        b.Entity<StudyTask>().Property(t => t.Kind).HasConversion<string>();
        b.Entity<StudyTask>().Property(t => t.Status).HasConversion<string>();
        b.Entity<StudyTask>().HasIndex(t => new { t.UserId, t.Date });
        b.Entity<StudySession>().HasIndex(s => new { s.UserId, s.Date });
        b.Entity<Submission>().Property(s => s.Assist).HasConversion<string>();
        b.Entity<Submission>().HasIndex(s => new { s.UserId, s.CreatedAt });
        b.Entity<Exercise>().HasIndex(e => new { e.UserId, e.TaskId });
        b.Entity<DayState>().Property(d => d.Mode).HasConversion<string>();
        b.Entity<AiUsage>().HasIndex(a => new { a.UserId, a.Date }).IsUnique();
    }
}
