using System;
using System.Collections.Generic;
using System.Linq;

public class ChunkingService
{
    public List<Chunk> ChunkDocuments(IEnumerable<Document> docs, int maxChars = 300)
    {
        var allChunks = new List<Chunk>();

        foreach (var doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Content))
                continue;

            var docChunks = SplitIntoChunks(doc, maxChars);
            allChunks.AddRange(docChunks);
        }

        return allChunks;
    }

    private static List<Chunk> SplitIntoChunks(Document doc, int maxChars)
    {
        var chunks = new List<Chunk>();
        var content = doc.Content.Trim();

        // If content is short enough, keep as single chunk
        if (content.Length <= maxChars)
        {
            chunks.Add(new Chunk
            {
                DocumentId = doc.Id,
                ChunkId = 0,
                Title = doc.Title ?? string.Empty,
                Text = content
            });
            return chunks;
        }

        // Split by sentences (period + space)
        var sentences = content.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
        var currentText = "";
        int chunkId = 0;

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // If adding this sentence exceeds maxChars, flush current chunk
            if (currentText.Length > 0 && (currentText.Length + trimmed.Length + 2) > maxChars)
            {
                chunks.Add(new Chunk
                {
                    DocumentId = doc.Id,
                    ChunkId = chunkId++,
                    Title = doc.Title ?? string.Empty,
                    Text = currentText.Trim()
                });
                currentText = trimmed;
            }
            else
            {
                if (currentText.Length > 0)
                    currentText += ". ";
                currentText += trimmed;
            }
        }

        // Flush remaining text
        if (!string.IsNullOrWhiteSpace(currentText))
        {
            chunks.Add(new Chunk
            {
                DocumentId = doc.Id,
                ChunkId = chunkId,
                Title = doc.Title ?? string.Empty,
                Text = currentText.Trim()
            });
        }

        return chunks;
    }
}