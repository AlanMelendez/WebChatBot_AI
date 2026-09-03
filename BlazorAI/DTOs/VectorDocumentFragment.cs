using Microsoft.Extensions.VectorData;
using System.Numerics.Tensors;

namespace BlazorAI.DTOs
{
    public class VectorDocumentFragment
    {
        [VectorStoreKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        [VectorStoreData(IsIndexed = true)]
        public string DocumentTitle { get; set; } = string.Empty;


        [VectorStoreData(IsFullTextIndexed = true)]
        public string Text { get; set; } = string.Empty;


        [VectorStoreVector(dimensions:1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Embedding { get; set; } = ReadOnlyMemory<float>.Empty;

    }
}
