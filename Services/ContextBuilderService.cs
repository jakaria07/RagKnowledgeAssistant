public class ContextBuilderService
{
    public string Build(Document? doc)
    {
        if (doc == null)
            return "No relevant data found.";

        return $"{doc.Title}: {doc.Content}";
    }
}