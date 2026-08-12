using Anthropic;
using BlazorAI.Components;
using BlazorAI.Services;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IChatbot, FakeChatbot>();

builder.Services.AddChatClient(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = "openai";
    var model = configuration.GetValue<string>("ChatClient:Model") ?? "gpt-5.4-nano";

    var openAiApiKey = configuration.GetValue<string>("ChatClient:OpenAI:ApiKey");
    var claudeKey = configuration.GetValue<string>("ChatClient:Anthropic:ApiKey");

    IChatClient client = provider switch
    {
        "openai" => (IChatClient)new OpenAI.Chat.ChatClient(openAiApiKey, model),
        "claude" => (IChatClient)new AnthropicClient() { ApiKey = claudeKey }
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(chat => chat.ModelId = model ?? "claude-haiku-5")
            .Build(),
        _ => throw new InvalidOperationException($"Unsupported chat client provider: {provider}")
    };

    return client
        .AsBuilder()
        .ConfigureOptions(
            opt =>
            {
                opt.MaxOutputTokens = 2000;
                opt.Temperature = 0.7f;
            }
        ).UseFunctionInvocation(null, c => c.IncludeDetailedErrors = true)
        .Build(sp);


    ;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
