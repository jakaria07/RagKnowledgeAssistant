using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public EmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY environment variable is not set.");
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        Console.WriteLine($"[DEBUG] Embedding text: \"{text}\"");

        var embedContentBody = JsonSerializer.Serialize(new
        {
            content = new
            {
                parts = new[]
                {
                    new { text = text }
                }
            }
        });

        var embedTextBody = JsonSerializer.Serialize(new
        {
            text = text
        });

        var candidates = new List<(string Url, string Body)>
        {
            ($"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent?key={_apiKey}", embedContentBody),
            ($"https://generativelanguage.googleapis.com/v1/models/text-embedding-004:embedContent?key={_apiKey}", embedContentBody),
            ($"https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedContent?key={_apiKey}", embedContentBody),
            ($"https://generativelanguage.googleapis.com/v1beta/models/embedding-001:embedText?key={_apiKey}", embedTextBody),
        };

        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            Console.WriteLine($"[DEBUG] Gemini embedding candidate URL: {RedactKey(candidate.Url)}");
            Console.WriteLine($"[DEBUG] Gemini request body: {candidate.Body}");

            using var httpContent = new StringContent(candidate.Body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(candidate.Url, httpContent);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[DEBUG] Gemini response status: {response.StatusCode}");
            Console.WriteLine($"[DEBUG] Gemini response body: {jsonResponse}");

            if (!response.IsSuccessStatusCode)
            {
                errors.Add($"{response.StatusCode} for {RedactKey(candidate.Url)} => {jsonResponse}");
                continue;
            }

            var result = JsonSerializer.Deserialize<EmbeddingResponse>(jsonResponse);
            var values = result?.Embedding?.Values ?? result?.Embedding?.Value;

            if (values == null || values.Count == 0)
            {
                errors.Add($"No embedding values returned for {RedactKey(candidate.Url)} => {jsonResponse}");
                continue;
            }

            return values.ToArray();
        }

        var modelsV1 = await TryListModelsAsync("v1");
        var modelsV1Beta = await TryListModelsAsync("v1beta");

        throw new InvalidOperationException(
            "Gemini embedding API error: No embedding endpoint succeeded. " +
            "Tried: " + string.Join(" | ", errors) + "\n" +
            "ListModels(v1): " + modelsV1 + "\n" +
            "ListModels(v1beta): " + modelsV1Beta);
    }

    private async Task<string> TryListModelsAsync(string version)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/{version}/models?key={_apiKey}";
            using var response = await _httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return $"{response.StatusCode} => {body}";

            return body;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string RedactKey(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        var idx = url.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return url;

        return url.Substring(0, idx + 4) + "***";
    }

    private class EmbeddingResponse
    {
        public EmbeddingData? Embedding { get; set; }
    }

    private class EmbeddingData
    {
        public List<float>? Values { get; set; }
        public List<float>? Value { get; set; }
    }
}