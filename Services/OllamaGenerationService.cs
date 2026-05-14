using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OllamaGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _maxTokens;

    public OllamaGenerationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        _model = Environment.GetEnvironmentVariable("OLLAMA_GEN_MODEL") ?? "phi3:mini";
        // Allow up to 280 tokens for more elaborate responses while staying within 8GB RAM limits
        _maxTokens = int.TryParse(Environment.GetEnvironmentVariable("OLLAMA_GEN_MAX_TOKENS"), out var n)
            ? n
            : 280;
    }

    public async Task<string> GenerateAsync(string query, string context)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var url = _baseUrl.TrimEnd('/') + "/api/generate";

        // Prompt for Phi3:mini with elaboration encouragement
        // Encourages detailed responses using context details (times, purposes, hosts, status)
        var prompt =
            "Answer using ONLY the provided context. Include relevant details like visit times, purposes, or hosts when available.\n" +
            "Provide a natural, complete response - not just a simple list.\n" +
            "If context is insufficient, say 'I don't know'.\n\n" +
            "Context:\n" + context + "\n\n" +
            "Q: " + query + "\n" +
            "A:";

        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt = prompt,
            stream = false,
            options = new
            {
                num_predict = _maxTokens,
                temperature = 0.1,  // Even lower temperature for more deterministic responses
                top_p = 0.9
            }
        });

        using var contentObj = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, contentObj);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama generation error: {(int)response.StatusCode} {response.StatusCode} => {json}");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("response", out var responseEl) && responseEl.ValueKind == JsonValueKind.String)
            return responseEl.GetString() ?? string.Empty;

        return string.Empty;
    }
}
