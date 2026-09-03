using Anthropic;
using BlazorAI;
using BlazorAI.Components;
using BlazorAI.Services;
using Microsoft.Extensions.AI;
using static BlazorAI.Tools;
using static BlazorAI.Extensions.ServiceExtensions;
using BlazorAI.Data;
using Microsoft.EntityFrameworkCore;
using BlazorAI.Services.RAG;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCommonServices();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite("Data Source=mydb.db"));


builder.Services.AddChatClient(sp =>
{


    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = "openai";
    var model = "gpt-5.4-nano";

    var openAiApiKey = configuration.GetValue<string>("OPENAIKEY");
    var claudeKey = configuration.GetValue<string>("ChatClient:Anthropic:ApiKey");

    IChatClient client = provider switch
    {
        "openai" => new OpenAI.Chat.ChatClient(model,openAiApiKey).AsIChatClient(),
        "claude" => new AnthropicClient() { ApiKey = claudeKey }
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(chat => chat.ModelId = model ?? "claude-haiku-5")
            .Build(),
        _ => throw new InvalidOperationException($"Unsupported chat client provider: {provider}")
    };

    return client
        .AsBuilder()
        .UseFunctionInvocation(null, c => c.IncludeDetailedErrors = true)
        .Build(sp);


    ;
});

builder.Services.AddTransient<ChatOptions>(sp => new ChatOptions
{
    MaxOutputTokens = 2000,
    Temperature = 0.7f,
    Tools = [.. Tools.GetTools(sp)]
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
