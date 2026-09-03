using BlazorAI.DTOs;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;

namespace BlazorAI.Services.RAG
{
    public class FakeRAGService : IRAGService
    {
        public readonly DocumentsFromMemoryService _documentsFromMemoryService;
        public readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public readonly InMemoryCollection<Guid, VectorDocumentFragment> _vectorStore;

        private bool _isInitialized = false;

        public FakeRAGService(DocumentsFromMemoryService documentsFromMemoryService, 
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            InMemoryVectorStore vectorStore) { 
        
            _documentsFromMemoryService = documentsFromMemoryService;
            _embeddingGenerator = embeddingGenerator;


            _vectorStore = vectorStore.GetCollection<Guid, VectorDocumentFragment>("documents");
        }

        public async Task<List<string>> FindRelevantContext(string prompt, int topK = 3, CancellationToken cancellationToken = default)
        {
            await Initialize(cancellationToken);

            var promptEmbedding = await _embeddingGenerator.GenerateVectorAsync(prompt, null, cancellationToken);


            var results = new List<string>();


            await  foreach (var document in _vectorStore.SearchAsync(promptEmbedding, topK, null, cancellationToken)) {

                results.Add($"""

                    Document Title: {document.Record.DocumentTitle}
                    Text: {document.Record.Text}

                    """);
            }

            return results;
        }

        private async Task Initialize(CancellationToken cancellationToken = default)
        {
           if (_isInitialized)
           {
                return;
           }

           await _vectorStore.EnsureCollectionExistsAsync(cancellationToken);

            var documents = _documentsFromMemoryService.GetDocuments();

            foreach (var document in documents)
            {

                var chunks = SplitIntoChunks(document.Content, 1000); // Split the document into chunks of 1000 characters
                
                foreach(var chunk in chunks)
                {

                    var vector = await _embeddingGenerator.GenerateVectorAsync(chunk,null, cancellationToken);

                    var record = new VectorDocumentFragment
                    {
                        Id = Guid.NewGuid(),
                        DocumentTitle = document.Title,
                        Text = chunk,
                        Embedding = vector
                    };

                    await _vectorStore.UpsertAsync(record, cancellationToken);

                }

            }

            _isInitialized = true;
        }

        private List<string> SplitIntoChunks (string text, int maxCharacters)
        {
            //First separete the text into paragraphs, then combine them into chunks of maxCharacters length
            var paragraphs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var result = new List<string>();
            var current = string.Empty;


            foreach (var paragraph in paragraphs)
            {

                var candiadate = string.IsNullOrWhiteSpace(current)
                    ? paragraph // Take the first paragraph as the initial chunk if current is empty
                    : $"{current}\n{paragraph}"; // Append the paragraph to the current chunk if it's not empty

                // Check if the candidate chunk exceeds the max character limit 
                if (candiadate.Length > maxCharacters)
                {

                    // If the candidate exceeds the max length, add the current chunk to the result and start a new chunk
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        result.Add(current);
                    }

                    // Start a new chunk with the current paragraph
                    current = paragraph;

                }
                else
                {
                    current = candiadate;

                }
            }

            if(!string.IsNullOrWhiteSpace(current))
            {
                result.Add(current);
            }

            return result;
        }
    }
}
