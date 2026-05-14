using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OllamaEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        _model = Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL") ?? "nomic-embed-text";
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        var url = _baseUrl.TrimEnd('/') + "/api/embeddings";

        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            prompt = text
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama embeddings error: {(int)response.StatusCode} {response.StatusCode} => {json}");

        var embedding = TryParseEmbedding(json);
        if (embedding.Length == 0)
            return Array.Empty<float>();

        return embedding;
    }

    private static float[] TryParseEmbedding(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(json, _jsonOptions);
            if (result?.Embedding != null && result.Embedding.Length > 0)
                return result.Embedding;
        }
        catch
        {
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("embedding", out var embeddingEl))
                return Array.Empty<float>();

            if (embeddingEl.ValueKind != JsonValueKind.Array)
                return Array.Empty<float>();

            var values = new float[embeddingEl.GetArrayLength()];
            var i = 0;
            foreach (var item in embeddingEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    values[i++] = item.GetSingle();
                }
                else
                {
                    return Array.Empty<float>();
                }
            }

            return values;
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    private sealed class OllamaEmbeddingResponse
    {
        public string Model { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
