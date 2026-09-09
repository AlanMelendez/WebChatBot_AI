namespace BlazorAI.Services.RAG
{
    public interface IRAGService
    {
        Task<List<string>> FindRelevantContext(string query, int topK = 3, float minScore = 0.6f, CancellationToken cancellationToken = default);
    }
}
