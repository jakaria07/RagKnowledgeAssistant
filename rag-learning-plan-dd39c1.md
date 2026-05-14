# RAG Learning Plan (2 Phases, .NET Core)

Build a minimal, end-to-end RAG-shaped .NET Core Web API in Phase 1 (fast + clear flow), then upgrade it to a minimum “real RAG” in Phase 2 (chunking + embeddings + top-k retrieval + LLM).

## Constraints / priorities

- Deliver something demoable fast (Phase 1) to show the full pipeline flow.
- Keep Phase 2 minimal (only what’s required to credibly call it RAG).
- Prefer simple data + clear architecture over feature depth.

## Current repo state (observed)

- `Program.cs` is currently a minimal template and does not register controllers/services yet.
- `Data/policies.json` exists with 2 sample policies.
- Folders exist: `Controllers/`, `Models/`, `Services/` (currently empty).

---

## Phase 1 — MVP “RAG-shaped” skeleton (lexical retrieval)

**Goal**: Implement and understand the pipeline boundaries:
**API → Retrieve → Build Context → Generate Response (+ Source)**

### Deliverables

1. **Data layer (JSON-based) expanded for the use case**
   - Keep `company policies`.
   - Add `past visitor data` dataset as a separate file: `Data/visitors.json` (minimal schema).
   - Ensure all data files are copied to output on build.

2. **Models**
   - `Document` (Id, Title, Content, optional `Category` / `Type`).
   - `RagRequest` (Query).
   - `RagResponse` (Answer, Sources).
     - Prefer `Sources` as a list (even if top-1 now) to support Phase 2.

3. **Services (separation of concerns)**
   - `IDataStore` or equivalent loader that reads JSON and returns in-memory docs.
   - `IRetrieverService` that returns top-1 (or top-k but can be 1 for Phase 1).
   - `ContextBuilderService` that builds a context string from retrieved docs.
   - `ResponseService` that generates a deterministic answer (template) and returns sources.

4. **API**
   - ASP.NET Core controller endpoint:
     - `POST /api/rag/query`
   - Validations:
     - reject empty query
     - handle “no match” clearly (avoid misleading answers)

5. **Demo assets**
   - A few example queries that use both datasets:
     - Policy Q: “Do visitors need admin approval?”
     - Visitor Q: “Who visited on <date>?” or “Show last 5 visitors”
   - Update `RagKnowledgeAssistant.http` (or keep a short test section in README) to demo quickly.

### “Understanding checkpoints” (what you should be able to explain)

- What exactly is being retrieved (documents/records) and why.
- Where relevance logic lives (keyword scoring) and its limitations.
- Why you separate retriever, context builder, and generator (easy upgrades).
- How you return sources and why it matters.

### Exit criteria (Phase 1 done)

- One endpoint answers questions using retrieved content.
- Response contains a **source** (policy title / visitor record id).
- You can demo 2–3 queries in under 2 minutes.

---

## Phase 2 — Minimum “real RAG” (chunking + embeddings + top-k + LLM)

**Goal**: Make retrieval semantic and generation LLM-based, while keeping everything minimal.

### Deliverables

1. **Chunking**
   - Convert each policy and any long visitor notes into chunks.
   - Store chunk metadata:
     - `DocumentId`, `ChunkId`, `Text`, and origin info (`Policy` vs `Visitor`).

2. **Embeddings**
   - Create embeddings for each chunk and the query.
   - Keep it minimal: use **local Ollama embeddings** (no billing required).
   - Recommended embedding model: `nomic-embed-text`.

   **Setup (Windows, from scratch)**
   - Install Ollama.
   - Start Ollama (it serves HTTP on `http://localhost:11434`).
   - Pull the embedding model:
     - `ollama pull nomic-embed-text`
   - Optional configuration via environment variables:
     - `OLLAMA_BASE_URL` (default: `http://localhost:11434`)
     - `OLLAMA_EMBED_MODEL` (default: `nomic-embed-text`)

3. **Vector search (minimal storage)**
   - MVP approach: in-memory vector list + cosine similarity (no external DB yet).
   - Retrieve **top-k chunks** (k=3..5).
   - Return sources per chunk.

4. **LLM-based generation**
   - Replace template response with an LLM call.
   - Implementation: keep `/api/rag/query` (Phase 1) unchanged and upgrade `/api/rag/query2` (Phase 2) to:
     - retrieve top-k chunks (Ollama embeddings + cosine similarity)
     - call a **local Ollama LLM** grounded on the retrieved chunk context
   - Environment variables:
     - `OLLAMA_BASE_URL` (default: `http://localhost:11434`)
     - `OLLAMA_GEN_MODEL` (default in code, e.g. `llama3`)
   - Fallback behavior:
     - if Ollama generation fails, return the retrieved context as a deterministic answer
   - Prompt structure:
     - System: “Answer only using the provided context. If insufficient, say you don’t know.”
     - User query
     - Retrieved context (top-k chunks)

5. **Citations**
   - Return citations as:
     - doc title + chunk id (and visitor record id where applicable)

6. **Minimal observability**
   - Log:
     - query
     - top-k sources
     - similarity scores
     - latency

### Exit criteria (Phase 2 done)

- Paraphrased queries work (semantic match, not exact keyword).
- Answers are produced by an LLM and include sources.
- System refuses / says “I don’t know” when context is missing.

---

## Decisions needed from you (not required to start Phase 1)

Before implementing Phase 2, confirm:

1. **Embedding + LLM provider**
    - Embeddings: **Ollama (local)** using `nomic-embed-text`.
    - LLM generation: **Ollama (local)**.

2. **Data format for visitor logs**
    - Recommended minimal schema (JSON) that matches your current `policies.json` approach:
      - `id` (int)
      - `title` (string) — a short human label, e.g. "Visitor: John Doe - 2026-05-01"
      - `content` (string) — a compact narrative line you can retrieve against
      - Optional later: `type` ("policy" | "visitor"), `timestamp`, `host`, etc.
    - Rationale: keeping a shared `Document` shape makes Phase 1 fast; Phase 2 can add metadata without breaking the flow.
    - File organization decision: **Option 1 confirmed** — keep visitor logs in a separate file (`Data/visitors.json`) rather than merging into a single knowledge file.

---

## Suggested demo narrative (30 seconds)

- Phase 1: “I built the RAG pipeline skeleton and API boundaries with sources, using company policies and visitor logs.”
- Phase 2: “I upgraded retrieval to embeddings (semantic) + top-k context and used an LLM to generate grounded answers with citations.”
