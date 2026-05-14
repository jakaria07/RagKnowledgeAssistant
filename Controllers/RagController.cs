using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/rag")]
public class RagController : ControllerBase
{
    private readonly IRetrieverService _retriever;
    private readonly ContextBuilderService _contextBuilder;
    private readonly ResponseService _responseService;
    private readonly SemanticRagService _semanticRag;

    public RagController(
        IRetrieverService retriever,
        ContextBuilderService contextBuilder,
        ResponseService responseService,
        SemanticRagService semanticRag)
    {
        _retriever = retriever;
        _contextBuilder = contextBuilder;
        _responseService = responseService;
        _semanticRag = semanticRag;
    }

    [HttpPost("query")]
    public IActionResult Query([FromBody] RagRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var doc = _retriever.Retrieve(request.Query);
        var context = _contextBuilder.Build(doc);
        var response = _responseService.Generate(context, doc);

        return Ok(response);
    }

    [HttpPost("query2")]
    public async Task<IActionResult> Query2([FromBody] RagRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var response = await _semanticRag.QueryAsync(request.Query, k: 8);
        return Ok(response);
    }
}