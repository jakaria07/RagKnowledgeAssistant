📄 Step 2: Add Sample Data
📁 Data/policies.json
[
  {
    "id": 1,
    "title": "Visitor Approval Policy",
    "content": "All visitors must be approved by an admin before entry."
  },
  {
    "id": 2,
    "title": "Working Hours",
    "content": "Office operates from 9 AM to 6 PM."
  }
]

👉 Right click file → Properties → set:
Copy to Output Directory → Copy if newer

(important, otherwise file won’t be found)

🧩 Step 3: Create Models
📄 Models/Document.cs
public class Document
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
}
📄 Models/RagRequest.cs
public class RagRequest
{
    public string Query { get; set; }
}
📄 Models/RagResponse.cs
public class RagResponse
{
    public string Answer { get; set; }
    public string Source { get; set; }
}
⚙️ Step 4: Create Services
📄 Services/IRetrieverService.cs
public interface IRetrieverService
{
    Document Retrieve(string query);
}
📄 Services/RetrieverService.cs

👉 This is your RAG core

using System.Text.Json;

public class RetrieverService : IRetrieverService
{
    private readonly List<Document> _documents;

    public RetrieverService()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "policies.json");
        var json = File.ReadAllText(filePath);
        _documents = JsonSerializer.Deserialize<List<Document>>(json) ?? new();
    }

    public Document Retrieve(string query)
    {
        query = query.ToLower();

        return _documents
            .OrderByDescending(d => MatchScore(d, query))
            .FirstOrDefault();
    }

    private int MatchScore(Document doc, string query)
    {
        int score = 0;

        if (doc.Title.ToLower().Contains(query)) score += 2;
        if (doc.Content.ToLower().Contains(query)) score += 1;

        return score;
    }
}
📄 Services/ContextBuilderService.cs
public class ContextBuilderService
{
    public string Build(Document doc)
    {
        if (doc == null)
            return "No relevant data found.";

        return $"{doc.Title}: {doc.Content}";
    }
}
📄 Services/ResponseService.cs
public class ResponseService
{
    public RagResponse Generate(string context)
    {
        return new RagResponse
        {
            Answer = $"Based on company data: {context}",
            Source = context.Split(":")[0]
        };
    }
}
🌐 Step 5: Create Controller
📄 Controllers/RagController.cs
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/rag")]
public class RagController : ControllerBase
{
    private readonly IRetrieverService _retriever;
    private readonly ContextBuilderService _contextBuilder;
    private readonly ResponseService _responseService;

    public RagController(
        IRetrieverService retriever,
        ContextBuilderService contextBuilder,
        ResponseService responseService)
    {
        _retriever = retriever;
        _contextBuilder = contextBuilder;
        _responseService = responseService;
    }

    [HttpPost("query")]
    public IActionResult Query([FromBody] RagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var doc = _retriever.Retrieve(request.Query);
        var context = _contextBuilder.Build(doc);
        var response = _responseService.Generate(context);

        return Ok(response);
    }
}
⚙️ Step 6: Register Services
📄 Program.cs

Replace with:

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IRetrieverService, RetrieverService>();
builder.Services.AddSingleton<ContextBuilderService>();
builder.Services.AddSingleton<ResponseService>();

var app = builder.Build();

app.MapControllers();

app.Run();
🚀 Step 7: Run & Test
dotnet run

Open Postman / Thunder Client:

POST:
http://localhost:xxxx/api/rag/query
Body:
{
  "query": "visitor approval"
}
✅ Expected Response
{
  "answer": "Based on company data: Visitor Approval Policy: All visitors must be approved by an admin before entry.",
  "source": "Visitor Approval Policy"
}