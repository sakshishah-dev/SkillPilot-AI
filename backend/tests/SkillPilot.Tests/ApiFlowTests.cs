using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SkillPilot.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"skillpilot-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={_db}",
            ["Jwt:Key"] = "test-key-test-key-test-key-test-key-1234",
            ["Registration:Open"] = "true",
            ["Ai:Anthropic:ApiKey"] = "",
            ["Ai:OpenAi:ApiKey"] = ""
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_db); } catch (IOException) { }
    }
}

public class ApiFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public ApiFlowTests(ApiFactory f) => _f = f;

    private static JsonElement J(string s) => JsonDocument.Parse(s).RootElement.Clone();

    private async Task<HttpClient> SignedIn(string email)
    {
        var c = _f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/register", new { email, password = "correct horse battery", displayName = "Tester" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var token = J(await r.Content.ReadAsStringAsync()).GetProperty("token").GetString()!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Endpoints_require_sign_in()
    {
        var c = _f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/plan/today")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/skills")).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_duplicate_email_are_rejected()
    {
        await SignedIn("dup@example.com");
        var c = _f.CreateClient();
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "dup@example.com", password = "wrong password" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var again = await c.PostAsJsonAsync("/api/auth/register", new { email = "dup@example.com", password = "correct horse battery", displayName = "x" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Full_daily_learning_flow_works_and_records_assist_honestly()
    {
        var c = await SignedIn("flow@example.com");

        // plan exists and respects the daily budget
        var today = J(await c.GetStringAsync("/api/plan/today"));
        var tasks = today.GetProperty("tasks").EnumerateArray().ToList();
        Assert.NotEmpty(tasks);
        Assert.True(today.GetProperty("plannedMinutes").GetInt32() <= today.GetProperty("availableMinutes").GetInt32());

        // finish a Learn task
        var learn = tasks.First(t => t.GetProperty("kind").GetString() == "Learn");
        var learnId = learn.GetProperty("id").GetString();
        var done = await c.PostAsJsonAsync($"/api/tasks/{learnId}/complete", new { minutes = 25, note = "read the lesson" });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        // time was logged
        var sessions = J(await c.GetStringAsync("/api/sessions?days=7"));
        Assert.Equal(25, sessions.GetProperty("sessions")[0].GetProperty("minutes").GetInt32());

        // find a practice task (maybe in a later day)
        var week = J(await c.GetStringAsync("/api/plan/week"));
        var practice = week.GetProperty("days").EnumerateArray().SelectMany(d => d.GetProperty("tasks").EnumerateArray())
            .First(t => t.GetProperty("kind").GetString() == "Practice");
        var pid = practice.GetProperty("id").GetString();

        var ex = J(await (await c.PostAsync($"/api/tasks/{pid}/exercise", null)).Content.ReadAsStringAsync());
        var exId = ex.GetProperty("id").GetString();
        Assert.Equal("Independent", ex.GetProperty("assist").GetString());
        Assert.False(ex.GetProperty("canViewSolution").GetBoolean());
        Assert.False(ex.ToString().Contains("\"solution\""), "solution must not be sent with the exercise");

        // solution is locked before an attempt
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync($"/api/exercises/{exId}/solution", null)).StatusCode);

        // a hint changes the assist level
        var hinted = J(await (await c.PostAsync($"/api/exercises/{exId}/hint", null)).Content.ReadAsStringAsync());
        Assert.Equal("Hint", hinted.GetProperty("assist").GetString());
        Assert.Equal(1, hinted.GetProperty("hintsUsed").GetInt32());

        // submit: offline reviewer is unverified, so mastery stays low
        var sub = await c.PostAsJsonAsync($"/api/exercises/{exId}/submit", new { code = "var x = 1; // my honest attempt at the task" });
        var subJson = J(await sub.Content.ReadAsStringAsync());
        Assert.False(subJson.GetProperty("review").GetProperty("verified").GetBoolean());
        Assert.True(subJson.GetProperty("mastery").GetInt32() < 70);

        // now the solution unlocks, and viewing it downgrades the credit
        var sol = await c.PostAsync($"/api/exercises/{exId}/solution", null);
        Assert.Equal(HttpStatusCode.OK, sol.StatusCode);

        // coach and weekly review respond
        var coach = J(await c.GetStringAsync("/api/coach/tomorrow"));
        Assert.True(coach.TryGetProperty("tomorrow", out _));
        var review = J(await c.GetStringAsync("/api/review/week"));
        Assert.Equal(25, review.GetProperty("studiedMinutes").GetInt32());
        Assert.InRange(review.GetProperty("consistency").GetInt32(), 0, 100);
    }

    [Fact]
    public async Task Users_cannot_touch_each_others_tasks_or_exercises()
    {
        var a = await SignedIn("a@example.com");
        var b = await SignedIn("b@example.com");
        var tasks = J(await a.GetStringAsync("/api/plan/today")).GetProperty("tasks");
        var id = tasks[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/tasks/{id}/skip", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/exercises/{Guid.NewGuid()}/hint", null)).StatusCode);
    }

    [Fact]
    public async Task Busy_day_rebuilds_today_without_piling_onto_tomorrow()
    {
        var c = await SignedIn("busy@example.com");
        var normal = J(await c.GetStringAsync("/api/plan/week"));
        var tomorrowBefore = normal.GetProperty("days")[1].GetProperty("tasks").GetArrayLength();

        var res = await c.PostAsJsonAsync("/api/plan/day-mode", new { mode = "Busy" });
        var today = J(await res.Content.ReadAsStringAsync());
        Assert.Equal("Busy", today.GetProperty("mode").GetString());
        Assert.True(today.GetProperty("plannedMinutes").GetInt32() <= 20);

        var after = J(await c.GetStringAsync("/api/plan/week"));
        Assert.True(after.GetProperty("days")[1].GetProperty("tasks").GetArrayLength() >= tomorrowBefore - 1);
        var changes = J(await c.GetStringAsync("/api/plan/changes"));
        Assert.Contains("busy day", changes[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Profile_validation_rejects_bad_values_and_confidence_is_stored_separately()
    {
        var c = await SignedIn("prof@example.com");
        var bad = await c.PutAsJsonAsync("/api/profile", new { availability = new[] { 1, 2, 3 } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var ok = await c.PutAsJsonAsync("/api/profile/confidence", new[] { new { skillKey = "csharp", confidence = 8 } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var skills = J(await c.GetStringAsync("/api/skills"));
        var cs = skills.EnumerateArray().First(s => s.GetProperty("key").GetString() == "csharp");
        Assert.Equal(8, cs.GetProperty("confidence").GetInt32());
        Assert.Equal(0, cs.GetProperty("mastery").GetInt32());
        Assert.Equal(80, cs.GetProperty("gap").GetInt32());
    }
}
