# RagKnowledgeAssistant - Architecture Documentation

## Overview

RagKnowledgeAssistant is a Retrieval-Augmented Generation (RAG) system built with ASP.NET Core that provides semantic search and question-answering over company knowledge base data (policies and visitor logs). The system uses local Ollama models for both embeddings and LLM generation, ensuring full offline capability without external API dependencies.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         ASP.NET Core Web API                    │
│                    (http://localhost:5229)                      │
└────────────────────────┬────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
    Phase 1 Endpoint              Phase 2 Endpoint
  /api/rag/query                 /api/rag/query2
         │                               │
         │                               │
    ┌────▼─────────┐                ┌────▼────────────────────────┐
    │ RagController│                │    RagController            │
    └────┬─────────┘                └────┬────────────────────────┘
         │                               │
         │                               │
    ┌────▼──────────────────────┐  ┌─────▼────────────────────────┐
    │ Phase 1 Services          │  │ Phase 2 Services             │
    │ (Lexical Retrieval)       │  │ (Semantic Retrieval + LLM)   │
    ├───────────────────────────┤  ├──────────────────────────────┤
    │ - RetrieverService        │  │ - SemanticRagService         │
    │ - ContextBuilderService   │  │ - DocumentStoreService       │
    │ - ResponseService         │  │ - ChunkingService            │
    └───────────────────────────┘  │ - VectorStoreService         │
                                   │ - OllamaEmbeddingService     │
                                   │ - OllamaGenerationService    │
                                   └──────────────────────────────┘
                                             │
                                             │
                        ┌────────────────────┴───────────────────────┐
                        │                                            │
                   ┌────▼────────────┐                      ┌────────▼────────┐
                   │  Ollama Server  │                      │  Data Files     │
                   │  (localhost:    │                      │  - policies.json│
                   │   11434)        │                      │  - visitors.json│
                   ├─────────────────┤                      └─────────────────┘
                   │ - nomic-embed-  │
                   │   text (embed)  │
                   │ - phi3:mini     │
                   │   (generation)  │
                   └─────────────────┘
```

## Phase 1: Lexical Retrieval (MVP)

### Purpose
Phase 1 provides a simple keyword-based retrieval system as a minimal viable product to demonstrate RAG concepts.

### Components

#### 1. Data Layer
- **Document Model** (`Models/Document.cs`)
  - Properties: `Id`, `Title`, `Content`
  - Used for both policies and visitor records
- **Data Files**
  - `Data/policies.json` - Company policies
  - `Data/visitors.json` - Visitor log records

#### 2. Service Layer

**RetrieverService** (`Services/RetrieverService.cs`)
- Loads documents from JSON files
- Performs keyword-based search with simple tokenization
- Scores documents based on keyword matches in title and content
- Returns the highest-scoring document

**ContextBuilderService** (`Services/ContextBuilderService.cs`)
- Builds a context string from the retrieved document
- Formats context for response generation

**ResponseService** (`Services/ResponseService.cs`)
- Generates the final `RagResponse`
- Includes answer and source information

#### 3. Controller
**RagController** (`Controllers/RagController.cs`)
- Exposes `POST /api/rag/query` endpoint
- Coordinates the Phase 1 pipeline

### Phase 1 Flow
```
User Query → RetrieverService → ContextBuilderService → ResponseService → RagResponse
```

## Phase 2: Semantic Retrieval + LLM Generation

### Purpose
Phase 2 implements a full semantic RAG pipeline with vector embeddings and LLM-based answer generation.

### Components

#### 1. Data Layer
- **Chunk Model** (`Models/Chunk.cs`)
  - Properties: `DocumentId`, `ChunkId`, `Title`, `Text`
  - Represents smaller segments of documents for embedding

#### 2. Service Layer

**DocumentStoreService** (`Services/DocumentStoreService.cs`)
- Centralizes document loading from JSON files
- Provides `LoadAll()` method to retrieve all documents
- Reused across both phases for consistency

**ChunkingService** (`Services/ChunkingService.cs`)
- Splits documents into smaller chunks
- Respects sentence boundaries where possible
- Configurable maximum character limit per chunk
- Returns list of chunks with metadata

**OllamaEmbeddingService** (`Services/OllamaEmbeddingService.cs`)
- Interacts with local Ollama server for text embeddings
- Calls `POST /api/embeddings` endpoint
- Uses `nomic-embed-text` model by default
- Environment variables:
  - `OLLAMA_BASE_URL` (default: `http://localhost:11434`)
  - `OLLAMA_EMBED_MODEL` (default: `nomic-embed-text`)
- Handles JSON parsing with case-insensitive deserialization
- Fallback parsing via `JsonDocument` for robustness

**VectorStoreService** (`Services/VectorStoreService.cs`)
- Manages in-memory vector store
- Stores chunks with their embedding vectors
- Implements hybrid scoring:
  - **Primary**: Cosine similarity (semantic)
  - **Boost**: Lexical token overlap (0.3 weight)
- `AddChunksAsync()`: Pre-computes embeddings for chunks
- `SearchAsync()`: Retrieves top-k most relevant chunks
- Includes tokenization helpers for lexical scoring

**OllamaGenerationService** (`Services/OllamaGenerationService.cs`)
- Interacts with local Ollama server for LLM generation
- Calls `POST /api/generate` endpoint
- Uses `phi3:mini` model by default (lightweight for 8GB RAM)
- Environment variables:
  - `OLLAMA_BASE_URL` (default: `http://localhost:11434`)
  - `OLLAMA_GEN_MODEL` (default: `phi3:mini`)
  - `OLLAMA_GEN_MAX_TOKENS` (default: 256)
- Configurable generation options:
  - `num_predict`: Limits output length
  - `temperature`: Controls randomness (0.2 for deterministic)
- Builds grounded prompt with context and query
- Returns LLM-generated answer

**SemanticRagService** (`Services/SemanticRagService.cs`)
- Orchestrates the Phase 2 RAG pipeline
- Lazy initialization: Builds vector index on first query
- Pipeline:
  1. Load documents via `DocumentStoreService`
  2. Chunk documents via `ChunkingService`
  3. Compute embeddings via `OllamaEmbeddingService`
  4. Store vectors in `VectorStoreService`
  5. For each query:
     - Embed query
     - Retrieve top-k chunks via `VectorStoreService`
     - Build context from retrieved chunks
     - Generate answer via `OllamaGenerationService`
     - Return `RagResponse` with answer and sources
- Thread-safe initialization using `SemaphoreSlim`
- Fallback to context dump if LLM generation fails

#### 3. Controller
**RagController** (`Controllers/RagController.cs`)
- Exposes two endpoints:
  - `POST /api/rag/query` - Phase 1 (lexical)
  - `POST /api/rag/query2` - Phase 2 (semantic + LLM)
- Both endpoints accept `{ "query": "..." }` body
- Returns `{ "answer": "...", "sources": ["..."] }`

### Phase 2 Flow
```
User Query → SemanticRagService
              ↓
         Lazy Init (first query only)
              ↓
    Load Docs → Chunk → Embed → Store
              ↓
         Embed Query
              ↓
    Vector Search (top-k chunks)
              ↓
    Build Context
              ↓
    Ollama Generation (grounded)
              ↓
    RagResponse (answer + sources)
```

## Dependency Injection Configuration

**Program.cs** configures the service container:

```csharp
builder.Services.AddControllers();

// Phase 1 services
builder.Services.AddSingleton<IRetrieverService, RetrieverService>();
builder.Services.AddSingleton<ContextBuilderService>();
builder.Services.AddSingleton<ResponseService>();

// Phase 2 services
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddHttpClient<OllamaGenerationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddSingleton<DocumentStoreService>();
builder.Services.AddSingleton<ChunkingService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddSingleton<SemanticRagService>();
```

## Data Flow

### Document Loading
```
policies.json → DocumentStoreService.LoadAll() → List<Document>
visitors.json → DocumentStoreService.LoadAll() → List<Document>
```

### Chunking
```
Document → ChunkingService.ChunkDocuments() → List<Chunk>
```

### Embedding
```
Chunk.Text → OllamaEmbeddingService.GetEmbeddingAsync() → float[]
Query → OllamaEmbeddingService.GetEmbeddingAsync() → float[]
```

### Vector Search
```
Query + Stored Vectors → VectorStoreService.SearchAsync() → List<Chunk> (top-k)
```

### Generation
```
Query + Retrieved Chunks → OllamaGenerationService.GenerateAsync() → string (answer)
```

## Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `OLLAMA_BASE_URL` | Ollama server URL | `http://localhost:11434` |
| `OLLAMA_EMBED_MODEL` | Embedding model name | `nomic-embed-text` |
| `OLLAMA_GEN_MODEL` | Generation model name | `phi3:mini` |
| `OLLAMA_GEN_MAX_TOKENS` | Max tokens for generation | `256` |

## Ollama Models

### Embedding Model: `nomic-embed-text`
- Dimension: 768
- Lightweight, suitable for semantic search
- Pulled via: `ollama pull nomic-embed-text`

### Generation Model: `phi3:mini`
- Lightweight model suitable for 8GB RAM
- Configured for deterministic output (temperature: 0.2)
- Limited output length (num_predict: 256)
- Pulled via: `ollama pull phi3:mini`

## Retrieval Strategy

### Hybrid Scoring
The `VectorStoreService` uses a hybrid scoring approach:

```
Final Score = Cosine Similarity + (0.3 × Lexical Overlap Score)
```

- **Cosine Similarity**: Measures semantic alignment between query and chunk vectors
- **Lexical Overlap**: Measures token overlap between query and chunk text/title
- **Boost Weight (0.3)**: Ensures exact entity matches (e.g., "John Doe") rank higher

### Tokenization
- Splits on whitespace and common punctuation
- Case-insensitive matching
- Filters tokens shorter than 2 characters
- Uses `HashSet` for efficient overlap calculation

## Error Handling

### Ollama Service Errors
- Connection failures: Throws `InvalidOperationException` with details
- Timeout: Configured to 10 minutes for generation
- Fallback: Returns context dump if generation fails

### Empty Results
- No chunks stored: Returns empty list
- No relevant chunks: Returns "couldn't find relevant company data"
- Empty embeddings: Skips chunk storage

## Performance Considerations

### Lazy Initialization
- Vector index built on first query to avoid startup delay
- Subsequent queries use pre-built index
- Thread-safe using `SemaphoreSlim`

### Embedding Caching
- Chunk embeddings computed once at initialization
- Query embeddings computed per request
- In-memory storage (no persistence)

### Generation Timeout
- HttpClient timeout set to 10 minutes for slow hardware
- Configurable via `OLLAMA_GEN_MAX_TOKENS` for faster responses

## Security Considerations

### API Keys
- No external API keys required (Ollama is local)
- Environment variables used for configuration only

### Data Privacy
- All processing happens locally
- No data sent to external services
- Ollama runs on `localhost` only

## Future Enhancements

### Potential Improvements
1. **Persistent Vector Store**: Save embeddings to disk for faster startup
2. **Streaming Generation**: Use Ollama streaming for real-time response
3. **Advanced Chunking**: Implement semantic chunking
4. **Citations**: Add inline citations in generated answers
5. **Multi-Query**: Expand query with related terms for better retrieval
6. **Reranking**: Add cross-encoder reranking for top-k chunks
7. **Caching**: Cache query results for common questions
8. **Monitoring**: Add metrics for retrieval latency and accuracy

### Scalability
- For larger datasets: Consider vector databases (Qdrant, Milvus, pgvector)
- For higher throughput: Add request queuing and batch processing
- For distributed deployment: Consider microservices architecture

## Technology Stack

- **Framework**: ASP.NET Core 8.0
- **Language**: C# 12
- **Embeddings**: Ollama (nomic-embed-text)
- **LLM**: Ollama (phi3:mini)
- **Vector Search**: In-memory cosine similarity
- **Data Format**: JSON
- **HTTP Client**: System.Net.Http
- **JSON Serialization**: System.Text.Json

## File Structure

```
RagKnowledgeAssistant/
├── Controllers/
│   └── RagController.cs
├── Models/
│   ├── Chunk.cs
│   ├── Document.cs
│   ├── RagRequest.cs
│   └── RagResponse.cs
├── Services/
│   ├── ChunkingService.cs
│   ├── ContextBuilderService.cs
│   ├── DocumentStoreService.cs
│   ├── OllamaEmbeddingService.cs
│   ├── OllamaGenerationService.cs
│   ├── ResponseService.cs
│   ├── RetrieverService.cs
│   ├── SemanticRagService.cs
│   └── VectorStoreService.cs
├── Data/
│   ├── policies.json
│   └── visitors.json
├── Docs/
│   ├── architecture.md
│   └── Screenshots/
│       ├── query1.PNG
│       ├── query2_working hours.PNG
│       ├── query3_visitorApprovalPolicy.PNG
│       ├── query4_ContextOfTwoVisitors.PNG
│       ├── query5_johnDoeVisitedContext.PNG
│       └── query6_all_visitor_names.PNG
├── Properties/
│   └── launchSettings.json
├── Program.cs
├── RagKnowledgeAssistant.csproj
└── README.md
```
