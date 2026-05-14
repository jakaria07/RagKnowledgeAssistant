using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

public class RetrieverService : IRetrieverService
{
    private readonly List<Document> _documents;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public RetrieverService()
    {
        _documents = new List<Document>();

        var policies = LoadDocuments("policies.json");
        var visitors = LoadDocuments("visitors.json");

        _documents.AddRange(policies);
        _documents.AddRange(visitors);

        Console.WriteLine($"[DEBUG] Loaded {policies.Count} policies and {visitors.Count} visitors. Total docs: {_documents.Count}");
    }

    public Document? Retrieve(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        query = query.ToLowerInvariant();

        var tokens = Tokenize(query);
        Console.WriteLine($"[DEBUG] Query: {query} | Tokens: [{string.Join(", ", tokens)}]");

        var best = _documents
            .Select(d => new { Doc = d, Score = MatchScore(d, query) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (best != null)
            Console.WriteLine($"[DEBUG] Best match: Title='{best.Doc.Title}' Score={best.Score}");
        else
            Console.WriteLine("[DEBUG] No best match found");

        if (best == null || best.Score <= 0)
            return null;

        return best.Doc;
    }

    private static List<Document> LoadDocuments(string fileName)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", fileName);
        if (!File.Exists(filePath))
            return new List<Document>();

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<Document>>(json, JsonOptions) ?? new List<Document>();
    }

    private static int MatchScore(Document doc, string query)
    {
        if (doc == null) return 0;

        var title = (doc.Title ?? "").ToLowerInvariant();
        var content = (doc.Content ?? "").ToLowerInvariant();

        // Tokenize the query into meaningful keywords
        var tokens = Tokenize(query);

        if (tokens.Count == 0)
            return 0;

        int score = 0;

        foreach (var token in tokens.Distinct())
        {
            if (title.Contains(token)) score += 2;    // title hits are stronger
            if (content.Contains(token)) score += 1;  // content hits are weaker
        }

        if (score > 0)
        {
            var titlePreview = doc.Title.Length > 30 ? doc.Title.Substring(0, 30) + "..." : doc.Title;
            Console.WriteLine($"[DEBUG] Doc: '{titlePreview}' Score={score}");
        }

        return score;
    }

    // private static int MatchScore(Document doc, string query)
    // {
    //     int score = 0;

    //     if (doc.Title != null && doc.Title.ToLowerInvariant().Contains(query))
    //         score += 2;

    //     if (doc.Content != null && doc.Content.ToLowerInvariant().Contains(query))
    //         score += 1;

    //     return score;
    // }

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","is","are","was","were","am",
        "to","of","in","on","at","for","from","by","with",
        "and","or","but",
        "me","my","you","your",
        "tell","show","find","check",
        "if","there","any","was","were",
        "please"
    };

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Split on non-letter/digit using simple regex
        var parts = System.Text.RegularExpressions.Regex.Split(text, @"[^a-zA-Z0-9]+");
        var tokens = new List<string>();

        foreach (var part in parts)
        {
            var token = part.Trim().ToLowerInvariant();
            if (token.Length < 3) continue;
            if (Stopwords.Contains(token)) continue;
            tokens.Add(token);
        }

        return tokens;
    }
}