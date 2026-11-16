using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SkillPilot.Api.Ai;
using SkillPilot.Api.Data;
using SkillPilot.Api.Security;
using SkillPilot.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from environment variables or user-secrets, never from source control.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set to a random string of at least 32 characters (environment variable Jwt__Key).");

builder.Services.AddDbContext<AppDbContext>((sp, o) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var provider = cfg.GetValue<string>("Database:Provider") ?? "sqlite";
    var connection = cfg.GetConnectionString("Default") ?? "Data Source=skillpilot.db";
    if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase)) o.UseNpgsql(connection);
    else o.UseSqlite(connection);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
// Read the key through IOptions so every consumer (signing and validating) sees the same value.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((o, opts) =>
    {
        var j = opts.Value;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = j.Issuer,
            ValidateAudience = true, ValidAudience = j.Issuer,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtService.KeyFrom(j.Key),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1) }));
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpClient<AnthropicProvider>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<OpenAiProvider>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<AnthropicProvider>());
builder.Services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<OpenAiProvider>());
builder.Services.AddScoped<TutorService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<LearningService>();
builder.Services.AddSingleton<JwtService>();

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler(a => a.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (ex is not (AiLimitException or UnauthorizedAccessException or ArgumentException))
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Unhandled").LogError(ex, "Unhandled error");
    var (status, message) = ex switch
    {
        AiLimitException e => (429, e.Message),
        UnauthorizedAccessException => (401, "Please sign in again."),
        ArgumentException e => (400, e.Message),
        _ => (500, "Something went wrong on the server.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { error = message });
}));

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

using (var scope = app.Services.CreateScope())
    await DbSeeder.InitAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.Run();

public partial class Program { }
