var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddHttpClient<OllamaGenerationService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddSingleton<DocumentStoreService>();
builder.Services.AddSingleton<ChunkingService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddSingleton<SemanticRagService>();
builder.Services.AddSingleton<IRetrieverService, RetrieverService>();
builder.Services.AddSingleton<ContextBuilderService>();
builder.Services.AddSingleton<ResponseService>();

var app = builder.Build();

app.MapControllers();

app.Run();