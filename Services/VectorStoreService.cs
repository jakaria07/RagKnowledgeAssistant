using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class VectorStoreService
{
    private readonly OllamaEmbeddingService _embeddingService;
    private readonly List<(Chunk Chunk, float[] Vector)> _storedChunks;

    // Optimized for 8GB RAM: Increased from 0.15 to 0.35 to boost keyword matching
    // This helps complex queries retrieve relevant chunks based on entity names (john, doe, maria, khan)
    private const float LexicalBoostWeight = 0.35f;

    // Common English stop words to filter out for better lexical matching
    private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
        "from", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do",
        "does", "did", "will", "would", "could", "should", "may", "might", "can", "if", "then",
        "so", "as", "which", "who", "what", "when", "where", "why", "how", "there", "that",
        "this", "it", "its", "any", "all", "each", "every", "more", "most", "some", "very",
        "you", "me", "i", "we", "they", "him", "her", "us", "them", "my", "your", "his", "our", "their"
    };

    public VectorStoreService(OllamaEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
        _storedChunks = new List<(Chunk, float[])>();
    }

    // Called at startup to load all chunks and pre-compute their vectors
    public async Task AddChunksAsync(IEnumerable<Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var vector = await _embeddingService.GetEmbeddingAsync(chunk.Text);
            if (vector.Length > 0)
            {
                _storedChunks.Add((chunk, vector));
            }
        }
    }

    public async Task<List<Chunk>> SearchAsync(string query, int k = 8)
    {
        if (_storedChunks.Count == 0)
            return new List<Chunk>();

        var queryVector = await _embeddingService.GetEmbeddingAsync(query);
        if (queryVector.Length == 0)
            return new List<Chunk>();

        // Retrieve top-k chunks using hybrid scoring (cosine + lexical)
        // Optimized for 8GB RAM: k=8 handles up to 7 visitors + 1 policy chunk with safety margin
        var scored = _storedChunks
            .Select(item => new
            {
                Chunk = item.Chunk,
                Score = HybridScore(query, queryVector, item.Chunk, item.Vector)
            })
            .Where(x => !float.IsNaN(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(k)
            .Select(x => x.Chunk)
            .ToList();

        return scored;
    }

    private static float HybridScore(string query, float[] queryVector, Chunk chunk, float[] chunkVector)
    {
        var cosine = CosineSimilarity(queryVector, chunkVector);
        var lexical = LexicalOverlapScore(query, chunk.Title + " " + chunk.Text);

        return cosine + (LexicalBoostWeight * lexical);
    }

    private static float LexicalOverlapScore(string query, string text)
    {
        var qTokens = Tokenize(query);
        if (qTokens.Count == 0)
            return 0f;

        var tTokens = Tokenize(text);
        if (tTokens.Count == 0)
            return 0f;

        var overlap = qTokens.Count(token => tTokens.Contains(token));
        return overlap / (float)qTokens.Count;
    }

    private static HashSet<string> Tokenize(string input)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input))
            return result;

        var parts = input
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in parts)
        {
            var token = p.Trim();
            // Filter stop words and require minimum token length
            // Stop word filtering improves lexical overlap scoring by focusing on meaningful keywords
            if (token.Length >= 2 && !StopWords.Contains(token))
                result.Add(token);
        }

        return result;
    }

    private static float CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length)
            return 0f;

        float dotProduct = 0;
        float magnitude1 = 0;
        float magnitude2 = 0;

        for (int i = 0; i < v1.Length; i++)
        {
            dotProduct += v1[i] * v2[i];
            magnitude1 += v1[i] * v1[i];
            magnitude2 += v2[i] * v2[i];
        }

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0f;

        return dotProduct / ((float)Math.Sqrt(magnitude1) * (float)Math.Sqrt(magnitude2));
    }
}