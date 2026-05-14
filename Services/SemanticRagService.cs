using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class SemanticRagService
{
    private readonly DocumentStoreService _documentStore;
    private readonly ChunkingService _chunkingService;
    private readonly VectorStoreService _vectorStore;
    private readonly OllamaGenerationService _generationService;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    // Optimized for 8GB RAM + Phi3:mini: limit context to 1800 chars to prevent slow generation
    // Longer contexts = slower inference on limited hardware
    private const int MaxContextCharacters = 2000;

    public SemanticRagService(
        DocumentStoreService documentStore,
        ChunkingService chunkingService,
        VectorStoreService vectorStore,
        OllamaGenerationService generationService)
    {
        _documentStore = documentStore;
        _chunkingService = chunkingService;
        _vectorStore = vectorStore;
        _generationService = generationService;
    }

    public async Task<RagResponse> QueryAsync(string query, int k = 8)
    {
        Console.WriteLine($"[DEBUG] SemanticRagService.QueryAsync called with query: \"{query}\"");
        if (string.IsNullOrWhiteSpace(query))
        {
            return new RagResponse
            {
                Answer = "Query is required.",
                Sources = new List<string>()
            };
        }

        await EnsureInitializedAsync();

        var topChunks = await _vectorStore.SearchAsync(query, k);
        if (topChunks.Count == 0)
        {
            return new RagResponse
            {
                Answer = "I couldn't find relevant company data for your question.",
                Sources = new List<string>()
            };
        }

        var context = string.Join("\n\n", topChunks.Select(c => $"{c.Title} (chunk {c.ChunkId}): {c.Text}"));
        
        // Limit context window for 8GB RAM + Phi3:mini performance
        if (context.Length > MaxContextCharacters)
        {
            context = context.Substring(0, MaxContextCharacters) + "...";
            Console.WriteLine($"[DEBUG] Context truncated from {context.Length} to {MaxContextCharacters} chars for Phi3:mini");
        }
        
        var sources = topChunks.Select(c => $"{c.Title}#chunk-{c.ChunkId}").Distinct().ToList();

        string answer;
        try
        {
            answer = await _generationService.GenerateAsync(query, context);
            if (string.IsNullOrWhiteSpace(answer))
                answer = $"Based on company data:\n\n{context}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Ollama generation failed: {ex.Message}");
            answer = $"Based on company data:\n\n{context}";
        }

        return new RagResponse
        {
            Answer = answer,
            Sources = sources
        };
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            Console.WriteLine("[DEBUG] SemanticRagService initializing...");
            var docs = _documentStore.LoadAll();
            Console.WriteLine($"[DEBUG] Loaded {docs.Count} documents");
            var chunks = _chunkingService.ChunkDocuments(docs);
            Console.WriteLine($"[DEBUG] Created {chunks.Count} chunks");
            await _vectorStore.AddChunksAsync(chunks);
            Console.WriteLine("[DEBUG] VectorStore initialized with embeddings");

            _initialized = true;
            Console.WriteLine("[DEBUG] SemanticRagService initialization complete");
        }
        finally
        {
            _initLock.Release();
        }
    }
}
