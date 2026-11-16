namespace SkillPilot.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public UserProfile? Profile { get; set; }
}

public class UserProfile
{
    public Guid UserId { get; set; }
    public string TargetRole { get; set; } = "Full-Stack Software Engineer (Applied AI)";
    public DateOnly? TargetDate { get; set; }
    /// <summary>JSON int[7]: study minutes Monday..Sunday.</summary>
    public string AvailabilityJson { get; set; } = "[90,90,90,90,90,180,180]";
    /// <summary>JSON string[]: skill keys in the order the user wants to learn them.</summary>
    public string PriorityJson { get; set; } = "[]";
    /// <summary>auto | anthropic | openai | offline</summary>
    public string AiProvider { get; set; } = "auto";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public bool ReminderEnabled { get; set; } = true;
    /// <summary>Minutes between check-ins during a running study session. 0 = off.</summary>
    public int ReminderIntervalMinutes { get; set; } = 30;
    public bool OnboardingDone { get; set; }
}

public class Skill
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = "";
    public string ResourceName { get; set; } = "";
    public string ResourceUrl { get; set; } = "";
}

public class Topic
{
    public string Id { get; set; } = "";
    public string SkillKey { get; set; } = "";
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = "";
    public int EstimatedMinutes { get; set; } = 60;
    public bool HasCode { get; set; } = true;
    public string Language { get; set; } = "csharp";
}

public class UserSkill
{
    public Guid UserId { get; set; }
    public string SkillKey { get; set; } = "";
    /// <summary>How confident the user FEELS, 0..10. Kept apart from demonstrated mastery.</summary>
    public int SelfConfidence { get; set; }
}

public class Evidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string SkillKey { get; set; } = "";
    public string TopicId { get; set; } = "";
    public EvidenceLevel Level { get; set; }
    public AssistMode Assist { get; set; }
    public int Score { get; set; } = 100;
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StudyTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public string TopicId { get; set; } = "";
    public StudyTaskKind Kind { get; set; }
    public StudyTaskStatus Status { get; set; } = StudyTaskStatus.Planned;
    public int PlannedMinutes { get; set; }
    public int Order { get; set; }
    public string Why { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
}

public class StudySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public string Note { get; set; } = "";
    /// <summary>1 low energy, 2 normal, 3 high.</summary>
    public int Energy { get; set; } = 2;
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? TaskId { get; set; }
    public string TopicId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string StarterCode { get; set; } = "";
    public string Language { get; set; } = "csharp";
    /// <summary>JSON string[]: progressive hints, weakest first.</summary>
    public string HintsJson { get; set; } = "[]";
    public string Rubric { get; set; } = "";
    /// <summary>Never sent to the client until the user has made an attempt and asks for it.</summary>
    public string SolutionText { get; set; } = "";
    public int HintsUsed { get; set; }
    public int TeacherAsks { get; set; }
    public bool SolutionViewed { get; set; }
    public int Attempts { get; set; }
    public string Source { get; set; } = "offline";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ExerciseId { get; set; }
    public Guid? TaskId { get; set; }
    public string TopicId { get; set; } = "";
    /// <summary>code | explain</summary>
    public string Kind { get; set; } = "code";
    public string Content { get; set; } = "";
    public AssistMode Assist { get; set; }
    public int Score { get; set; }
    public string ReviewJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Audit trail: every automatic change to the plan has a reason.</summary>
public class PlanChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class DayState
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public DayMode Mode { get; set; } = DayMode.Normal;
}

public class AiUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public int Calls { get; set; }
}
