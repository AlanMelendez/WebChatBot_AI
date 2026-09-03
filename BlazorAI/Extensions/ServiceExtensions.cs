using BlazorAI.Services;
using BlazorAI.Services.RAG;
using ChatbotSimple.Services;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using OpenAI.Embeddings;

namespace BlazorAI.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCommonServices(this IServiceCollection services)
        {


            services.AddKeyedScoped<IChatbot, RealChatBot>("chat");
            services.AddKeyedScoped<IChatbot, ChatbotRAG>("chat-rag");


            services.AddSingleton<DocumentsFromMemoryService>();
            services.AddSingleton<IRAGService,FakeRAGService>();
            services.AddSingleton<InMemoryVectorStore>();


            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(

                serviceProvider =>
                {
                    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                    var openAiApiKey = configuration.GetValue<string>("OPENAIKEY");
                    var embeddingModel = "text-embedding-3-small";


                    var client = new EmbeddingClient(embeddingModel, openAiApiKey).AsIEmbeddingGenerator();

                    return client;
                }
            );
            services.AddHttpClient();
            services.AddSingleton<IWeatherService, WeatherAPIService>();
            services.AddSingleton<EvaluateWeatherConditions>();
            services.AddSingleton<FakeGetEmailService>();
            services.AddSingleton<FakeSendEmailService>();
            services.AddScoped<IPersonService, PeopleService>();
            return services;
        }
    }
}
