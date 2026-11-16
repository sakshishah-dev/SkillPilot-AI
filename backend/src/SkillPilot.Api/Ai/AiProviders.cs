using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SkillPilot.Api.Ai;

public record AiMessage(string Role, string Content);

public class AiOptions
{
    public ProviderOptions Anthropic { get; set; } = new() { Model = "claude-sonnet-5-5" };
    public ProviderOptions OpenAi { get; set; } = new() { Model = "gpt-4o-mini" };
    /// <summary>Real AI calls allowed per user per day. Protects your bill.</summary>
    public int DailyCallLimit { get; set; } = 40;
}

public class ProviderOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}

public interface IAiProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<string> CompleteAsync(string system, IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);
}

public class AnthropicProvider(HttpClient http, Microsoft.Extensions.Options.IOptions<AiOptions> options) : IAiProvider
{
    private readonly ProviderOptions _o = options.Value.Anthropic;
    public string Name => "anthropic";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ApiKey);

    public async Task<string> CompleteAsync(string system, IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var body = new
        {
            model = _o.Model,
            max_tokens = maxTokens,
            system,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", _o.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new AiProviderException(Name, (int)res.StatusCode);

        using var doc = JsonDocument.Parse(text);
        var sb = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
            if (block.TryGetProperty("text", out var t)) sb.Append(t.GetString());
        return sb.ToString();
    }
}

public class OpenAiProvider(HttpClient http, Microsoft.Extensions.Options.IOptions<AiOptions> options) : IAiProvider
{
    private readonly ProviderOptions _o = options.Value.OpenAi;
    public string Name => "openai";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ApiKey);

    public async Task<string> CompleteAsync(string system, IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var all = new List<object> { new { role = "system", content = system } };
        all.AddRange(messages.Select(m => (object)new { role = m.Role, content = m.Content }));
        var body = new { model = _o.Model, max_tokens = maxTokens, messages = all };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);

        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new AiProviderException(Name, (int)res.StatusCode);

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

/// <summary>Provider failures never include the response body, so keys and prompts cannot leak into logs.</summary>
public class AiProviderException(string provider, int status)
    : Exception($"AI provider '{provider}' returned HTTP {status}.")
{
    public int Status { get; } = status;
}
