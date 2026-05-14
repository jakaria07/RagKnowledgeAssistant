public class ResponseService
{
    public RagResponse Generate(string context, Document? doc)
    {
        if (doc == null)
        {
            return new RagResponse
            {
                Answer = "I couldn't find relevant company data for your question.",
                Sources = new List<string>()
            };
        }

        return new RagResponse
        {
            Answer = $"Based on company data: {context}",
            Sources = new List<string> { doc.Title }
        };
    }
}