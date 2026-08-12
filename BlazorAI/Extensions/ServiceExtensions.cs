using BlazorAI.Services;
using ChatbotSimple.Services;

namespace BlazorAI.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCommonServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<IWeatherService, WeatherAPIService>();
            services.AddSingleton<EvaluateWeatherConditions>();
            services.AddSingleton<FakeGetEmailService>();
            services.AddSingleton<FakeSendEmailService>();





            return services;
        }
    }
}
