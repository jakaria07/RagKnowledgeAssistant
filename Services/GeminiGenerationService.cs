using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class GeminiGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiGenerationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
        _model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-2.0-flash";
    }

    public async Task<string> GenerateAsync(string query, string context)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY environment variable is not set.");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var body = JsonSerializer.Serialize(new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            text =
                                "You are a helpful assistant for a company knowledge base. " +
                                "Answer ONLY using the provided context. " +
                                "If the context is insufficient, say you don't know.\n\n" +
                                "Context:\n" + context + "\n\n" +
                                "Question: " + query
                        }
                    }
                }
            }
        });

        using var contentObj = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, contentObj);
        var json = await response.Content.ReadAsStringAsync();

        if ((int)response.StatusCode == 429)
            return string.Empty;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini generation error: {(int)response.StatusCode} {response.StatusCode} => {Truncate(json, 500)}");

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            return string.Empty;

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array ||
            parts.GetArrayLength() == 0)
            return string.Empty;

        var part0 = parts[0];
        if (part0.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            return textEl.GetString() ?? string.Empty;

        return string.Empty;
    }

    private static string Truncate(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length <= maxLen)
            return value;

        return value.Substring(0, maxLen);
    }
}
