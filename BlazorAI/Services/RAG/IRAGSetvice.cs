namespace BlazorAI.Services.RAG
{
    public interface IRAGService
    {
        Task<List<string>> FindRelevantContext(string query, int topK = 3, CancellationToken cancellationToken = default);
    }
}
