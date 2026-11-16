using System.ComponentModel.DataAnnotations;
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

public record CompleteRequest([Range(0, 600)] int Minutes = 0, [MaxLength(1000)] string? Note = null);
public record ExplainRequest([MinLength(10), MaxLength(6000)] string Text);
public record CodeRequest([MinLength(1), MaxLength(20000)] string Code);
public record AskRequest([MinLength(2), MaxLength(1500)] string Question, string? TopicId = null);

[ApiController, Authorize, Route("api/tasks")]
public class TasksController(AppDbContext db, PlanService plans, LearningService learning, TutorService tutor, ProgressService progress) : ControllerBase
{
    private Task<StudyTask?> Find(Guid id) => db.StudyTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == User.Id());

    public static string ExplainPrompt(Topic t) =>
        $"In your own words, explain \"{t.Title}\" as if to a teammate who has never used it. Say what it is, give one small example, and name one common mistake.";

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        var topic = Curriculum.Find(task.TopicId)!;
        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.UserId == task.UserId && e.TaskId == task.Id);
        var last = await db.Submissions.Where(s => s.TaskId == task.Id).OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync();
        return Ok(new
        {
            task = Dtos.Task(task),
            exercise = exercise == null ? null : Dtos.Exercise(exercise),
            explainPrompt = task.Kind == StudyTaskKind.Explain ? ExplainPrompt(topic) : null,
            lastReview = last == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(last.ReviewJson),
            lastContent = last?.Content
        });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteRequest req, CancellationToken ct)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (task.Kind != StudyTaskKind.Learn)
            return BadRequest(new { error = "Finish the exercise or explanation to complete this task." });
        if (task.Status == StudyTaskStatus.Done) return Ok(new { task = Dtos.Task(task) });

        task.Status = StudyTaskStatus.Done;
        task.CompletedAt = DateTime.UtcNow;
        var profile = await plans.ProfileAsync(task.UserId);
        if (req.Minutes > 0)
            db.Sessions.Add(new StudySession { UserId = task.UserId, Date = PlanService.TodayFor(profile), Minutes = req.Minutes, Note = req.Note ?? $"Learned: {Curriculum.Find(task.TopicId)!.Title}", TaskId = task.Id });
        await db.SaveChangesAsync();
        await learning.RecordAsync(task.UserId, task.TopicId, EvidenceLevel.Watched, AssistMode.Independent, 100, "Read the lesson");
        await plans.ReplanAsync(task.UserId, null);
        return Ok(new { task = Dtos.Task(task) });
    }

    [HttpPost("{id:guid}/skip")]
    public async Task<IActionResult> Skip(Guid id)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (task.Status == StudyTaskStatus.Done) return BadRequest(new { error = "That task is already done." });
        task.Status = StudyTaskStatus.Skipped;
        await db.SaveChangesAsync();
        await plans.ReplanAsync(task.UserId, $"You skipped \"{Curriculum.Find(task.TopicId)!.Title}\". It stays next in the queue and returns when there is room.");
        return Ok(new { ok = true });
    }

    [HttpPost("{id:guid}/exercise")]
    public async Task<IActionResult> StartExercise(Guid id, CancellationToken ct)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (task.Kind is not (StudyTaskKind.Practice or StudyTaskKind.Revision))
            return BadRequest(new { error = "This task has no coding exercise." });

        var existing = await db.Exercises.FirstOrDefaultAsync(e => e.UserId == task.UserId && e.TaskId == task.Id);
        if (existing != null) return Ok(Dtos.Exercise(existing));

        var topic = Curriculum.Find(task.TopicId)!;
        var profile = await plans.ProfileAsync(task.UserId);
        var prog = await progress.LoadAsync(task.UserId);
        var draft = await tutor.CreateExerciseAsync(profile, topic, prog[topic.Id].Mastery, await progress.LearnerContextAsync(task.UserId), ct);
        var ex = new Exercise
        {
            UserId = task.UserId, TaskId = task.Id, TopicId = topic.Id, Prompt = draft.Prompt, StarterCode = draft.StarterCode,
            Language = topic.Language, HintsJson = JsonSerializer.Serialize(draft.Hints), Rubric = draft.Rubric,
            SolutionText = draft.Solution, Source = draft.Source
        };
        db.Exercises.Add(ex);
        await db.SaveChangesAsync();
        return Ok(Dtos.Exercise(ex));
    }

    [HttpPost("{id:guid}/explain")]
    public async Task<IActionResult> Explain(Guid id, ExplainRequest req, CancellationToken ct)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (task.Kind != StudyTaskKind.Explain) return BadRequest(new { error = "This is not an explain task." });

        var topic = Curriculum.Find(task.TopicId)!;
        var profile = await plans.ProfileAsync(task.UserId);
        var review = await tutor.ReviewExplanationAsync(profile, topic, req.Text, ct);
        var (level, score) = LearningService.ExplainEvidence(review);

        db.Submissions.Add(new Submission
        {
            UserId = task.UserId, TaskId = task.Id, TopicId = topic.Id, Kind = "explain", Content = req.Text,
            Assist = AssistMode.Independent, Score = review.Score, ReviewJson = JsonSerializer.Serialize(LearningService.ReviewDto(review))
        });
        var done = review.Score >= 50 || !review.Verified && review.Score >= 50;
        if (done && task.Status != StudyTaskStatus.Done) { task.Status = StudyTaskStatus.Done; task.CompletedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        var mastery = await learning.RecordAsync(task.UserId, topic.Id, level, AssistMode.Independent, score, "Explained in own words");
        await plans.ReplanAsync(task.UserId, null);
        return Ok(new { review = LearningService.ReviewDto(review), taskDone = task.Status == StudyTaskStatus.Done, mastery });
    }
}

[ApiController, Authorize, Route("api/exercises")]
public class ExercisesController(AppDbContext db, PlanService plans, LearningService learning, TutorService tutor, ProgressService progress) : ControllerBase
{
    private Task<Exercise?> Find(Guid id) => db.Exercises.FirstOrDefaultAsync(e => e.Id == id && e.UserId == User.Id());

    [HttpPost("{id:guid}/hint")]
    public async Task<IActionResult> Hint(Guid id)
    {
        var ex = await Find(id);
        if (ex == null) return NotFound();
        if (ex.HintsUsed < Dtos.SafeHints(ex).Length) { ex.HintsUsed++; await db.SaveChangesAsync(); }
        return Ok(Dtos.Exercise(ex));
    }

    [HttpPost("{id:guid}/solution")]
    public async Task<IActionResult> Solution(Guid id)
    {
        var ex = await Find(id);
        if (ex == null) return NotFound();
        if (ex.Attempts < 1)
            return BadRequest(new { error = "Make an attempt first. A solution only teaches you after you have tried." });
        ex.SolutionViewed = true;
        await db.SaveChangesAsync();
        return Ok(new { exercise = Dtos.Exercise(ex), solution = ex.SolutionText });
    }

    [HttpPost("{id:guid}/ask")]
    public async Task<IActionResult> Ask(Guid id, AskRequest req, CancellationToken ct)
    {
        var ex = await Find(id);
        if (ex == null) return NotFound();
        var topic = Curriculum.Find(ex.TopicId)!;
        var profile = await plans.ProfileAsync(ex.UserId);
        var (answer, source) = await tutor.AskAsync(profile, topic, req.Question, await progress.LearnerContextAsync(ex.UserId), ex, ct);
        ex.TeacherAsks++;
        await db.SaveChangesAsync();
        return Ok(new { answer, source, exercise = Dtos.Exercise(ex) });
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CodeRequest req, CancellationToken ct)
    {
        var ex = await Find(id);
        if (ex == null) return NotFound();
        var profile = await plans.ProfileAsync(ex.UserId);

        var assist = Dtos.Assist(ex);
        var review = await tutor.ReviewCodeAsync(profile, ex, req.Code, ct);
        var (level, score) = LearningService.CodeEvidence(review);

        ex.Attempts++;
        db.Submissions.Add(new Submission
        {
            UserId = ex.UserId, ExerciseId = ex.Id, TaskId = ex.TaskId, TopicId = ex.TopicId, Kind = "code", Content = req.Code,
            Assist = assist, Score = review.Score, ReviewJson = JsonSerializer.Serialize(LearningService.ReviewDto(review))
        });

        var taskDone = false;
        if (ex.TaskId != null && review.Score >= 60)
        {
            var task = await db.StudyTasks.FirstOrDefaultAsync(t => t.Id == ex.TaskId && t.UserId == ex.UserId);
            if (task != null && task.Status != StudyTaskStatus.Done)
            {
                task.Status = StudyTaskStatus.Done; task.CompletedAt = DateTime.UtcNow; taskDone = true;
            }
        }
        await db.SaveChangesAsync();

        var mastery = await learning.RecordAsync(ex.UserId, ex.TopicId, level, assist, score, $"Practice attempt {ex.Attempts}");
        await plans.ReplanAsync(ex.UserId, null);
        return Ok(new
        {
            review = LearningService.ReviewDto(review), assist = assist.ToString(), mastery, taskDone,
            exercise = Dtos.Exercise(ex),
            credit = assist switch
            {
                AssistMode.Independent => "Full credit: you solved it without help.",
                AssistMode.Hint => "Reduced credit: you used hints.",
                AssistMode.Teacher => "Reduced credit: the tutor guided you.",
                _ => "Counts as watching only: you viewed the solution."
            }
        });
    }
}

[ApiController, Authorize, Route("api/tutor")]
public class TutorController(PlanService plans, TutorService tutor, ProgressService progress) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(AskRequest req, CancellationToken ct)
    {
        var uid = User.Id();
        var topic = Curriculum.Find(req.TopicId ?? "") ?? Curriculum.Topics.First();
        var profile = await plans.ProfileAsync(uid);
        var (answer, source) = await tutor.AskAsync(profile, topic, req.Question, await progress.LearnerContextAsync(uid), null, ct);
        return Ok(new { answer, source });
    }
}
