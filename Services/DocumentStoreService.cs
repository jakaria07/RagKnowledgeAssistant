using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class DocumentStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public List<Document> LoadAll()
    {
        var docs = new List<Document>();
        docs.AddRange(LoadDocuments("policies.json"));
        docs.AddRange(LoadDocuments("visitors.json"));
        return docs;
    }

    private static List<Document> LoadDocuments(string fileName)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", fileName);
        if (!File.Exists(filePath))
            return new List<Document>();

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<Document>>(json, JsonOptions) ?? new List<Document>();
    }
}
