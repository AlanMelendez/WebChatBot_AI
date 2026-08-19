# BlazorAI

BlazorAI is a .NET 10 Blazor Web App that demonstrates a conversational AI assistant with streaming responses, model tool calling, external weather data, SQLite-backed people data, and user approval for sensitive actions such as sending email.

The application uses Blazor Interactive Server rendering and `Microsoft.Extensions.AI` to connect the chat UI to an AI provider.

## Features

- Interactive chat interface at `/`.
- Streaming assistant responses rendered incrementally in the browser.
- Conversation state maintained by the scoped `RealChatBot` service.
- AI tool calling through `AIFunctionFactory`.
- Current weather lookup through WeatherAPI.
- Weather-condition evaluation for outdoor-activity recommendations.
- Fake email lookup based on a person's name.
- Fake email sending with approval required before execution.
- SQLite database access through Entity Framework Core.
- People lookup tool backed by the `People` table.
- Cancellation of an in-progress assistant response.
- Approval and rejection of sensitive tool calls from the chat UI.

> This is a demonstration project. The email services are intentionally fake and write to the console instead of sending real email.

## Technology stack

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 (`net10.0`) |
| Web framework | ASP.NET Core Blazor Web App |
| Rendering | Blazor Interactive Server |
| AI abstraction | Microsoft.Extensions.AI 10.9.0 |
| AI providers | OpenAI and Anthropic client integrations |
| OpenAI integration | `Microsoft.Extensions.AI.OpenAI` 10.9.0 |
| Anthropic integration | `Anthropic` 12.40.0 |
| Database | SQLite |
| ORM | Entity Framework Core 10.0.11 |
| Styling | Bootstrap assets and application CSS |
| Solution format | Visual Studio `.slnx` |

## Prerequisites

- .NET 10 SDK.
- An OpenAI API key for the default provider, or an Anthropic API key if the provider is changed in `Program.cs`.
- A WeatherAPI key if the weather tool is used.
- Visual Studio 2026 or the .NET CLI.

Verify the installed SDK:

```powershell
dotnet --version
```

## Getting started

1. Clone the repository and enter the project directory:

   ```powershell
   git clone https://github.com/AlanMelendez/WebChatBot_AI.git
   cd WebChatBot_AI
   ```

2. Configure the required secrets using .NET User Secrets. The project already contains a `UserSecretsId`:

   ```powershell
   dotnet user-secrets set "OPENAIKEY" "<your-openai-api-key>" --project .\BlazorAI\BlazorAI.csproj
   dotnet user-secrets set "WeatherAPIKey" "<your-weatherapi-key>" --project .\BlazorAI\BlazorAI.csproj
   ```

   If using the Anthropic branch in `Program.cs`, configure its key as well:

   ```powershell
   dotnet user-secrets set "ChatClient:Anthropic:ApiKey" "<your-anthropic-api-key>" --project .\BlazorAI\BlazorAI.csproj
   ```

3. Restore dependencies and build the project:

   ```powershell
   dotnet restore .\BlazorAI\BlazorAI.csproj
   dotnet build .\BlazorAI\BlazorAI.csproj
   ```

4. Start the application:

   ```powershell
   dotnet run --project .\BlazorAI\BlazorAI.csproj
   ```

5. Open the URL printed by ASP.NET Core. The configured development URLs are:

   - `https://localhost:7057`
   - `http://localhost:5012`

## AI provider configuration

The provider and model are currently selected in `BlazorAI/Program.cs`:

- Provider: `openai`
- Model: `gpt-5.4-nano`
- Maximum output tokens: `2000`
- Temperature: `0.7`

The OpenAI API key is read from `OPENAIKEY`. The Anthropic API key is read from `ChatClient:Anthropic:ApiKey`.

To use Anthropic, change the provider value in `Program.cs` from `openai` to `claude` and ensure the Anthropic secret is configured. The provider selection is currently source-code based rather than configuration based, so changing providers requires a code change.

## Available AI tools

Tools are registered in `BlazorAI/Tools.cs` and exposed to the selected chat client through `ChatOptions`.

| Tool | Purpose | Execution behavior |
| --- | --- | --- |
| `get_weather` | Gets the current condition for a city from WeatherAPI. | Executes automatically. |
| `evaluate_weather_conditions` | Interprets conditions such as rain, storms, snow, fog, sunny, or cloudy weather. | Executes automatically. |
| `GetEmail` | Returns a demonstration email address in the form `<name>@example.com`. | Executes automatically. |
| `SendEmail` | Demonstrates sending an email using body, subject, and recipient arguments. | Requires explicit approval in the UI. |
| `GetAll` | Loads all people from the SQLite database. | Executes automatically. |

The send-email tool is wrapped in `ApprovalRequiredAIFunction`. When the model requests it, the UI displays the tool name and arguments and provides **Approve** and **Reject** actions.

## Chat behavior

`RealChatBot` initializes each scoped chat session with a system prompt that instructs the assistant to:

- Answer general questions in English.
- Prefer concise responses.
- Return plain text rather than Markdown.
- Explain adjustments when recovering from tool errors.

Responses are streamed through `IChatClient.GetStreamingResponseAsync`. The Blazor page updates as text arrives and automatically scrolls to the newest message. A user can cancel an active response with the **Cancel** button.

## Data and Entity Framework Core

The application uses SQLite with the connection string configured directly in `Program.cs`:

```text
Data Source=mydb.db
```

The database file is located at `BlazorAI/mydb.db`. `ApplicationDbContext` exposes a `People` DbSet and seeds five sample people. The existing initial migration is in `BlazorAI/Migrations`.

To apply migrations manually:

```powershell
dotnet ef database update --project .\BlazorAI\BlazorAI.csproj
```

To create a new migration after changing the entity model:

```powershell
dotnet ef migrations add <MigrationName> --project .\BlazorAI\BlazorAI.csproj
```

## Project structure

```text
.
├── BlazorAI.slnx
├── README.md
└── BlazorAI
	├── Components
	│   ├── App.razor
	│   ├── Routes.razor
	│   ├── Layout
	│   └── Pages
	├── Data
	│   └── ApplicationDbContext.cs
	├── DTOs
	├── Entities
	├── Extensions
	│   └── ServiceExtensions.cs
	├── Migrations
	├── Services
	│   ├── RealChatBot.cs
	│   ├── WeatherAPIService.cs
	│   ├── PeopleService.cs
	│   └── Fake*Service.cs
	├── Tools.cs
	├── Program.cs
	├── BlazorAI.csproj
	└── wwwroot
```

## Service lifetimes

The application currently registers the main services as follows:

- `RealChatBot`: scoped, so conversation state is isolated to a Blazor circuit.
- `IWeatherService`: singleton.
- `EvaluateWeatherConditions`: singleton.
- `FakeGetEmailService`: singleton.
- `FakeSendEmailService`: singleton.
- `IPersonService`: scoped.
- `ApplicationDbContext`: created through `IDbContextFactory<ApplicationDbContext>`.

Tool creation resolves these services from the dependency-injection provider when `ChatOptions` is constructed. If a tool service is changed to scoped, tool construction must occur inside an appropriate scoped context rather than from the application root provider.

## Configuration reference

| Setting | Required | Description |
| --- | --- | --- |
| `OPENAIKEY` | Yes for OpenAI | OpenAI API key. Prefer User Secrets or environment variables. |
| `ChatClient:Anthropic:ApiKey` | Yes for Anthropic | Anthropic API key. |
| `WeatherAPIKey` | Yes for weather | WeatherAPI.com API key. |
| `AllowedHosts` | No | ASP.NET Core allowed-host configuration; currently `*`. |

Secrets should not be committed to `appsettings.json`, source code, or the repository. For production deployments, use a managed secret store or environment-specific secret configuration.

## Troubleshooting

### The application cannot resolve `IWeatherService`

Confirm that `ServiceExtensions.AddCommonServices()` is called from `Program.cs` and that `IWeatherService` is registered before chat tools are built. The current project registers `IWeatherService` as a singleton.

### The AI provider rejects the request

Check that the selected provider matches the configured API key, that the key is available to the active environment, and that the configured model is available to the account.

### Weather requests fail

Check `WeatherAPIKey`, network access, and the city name supplied to the assistant. WeatherAPI is called using the current-weather endpoint.

### Email tool execution stops for approval

This is expected. The send-email function intentionally pauses until the user approves or rejects the displayed request.

### SQLite data is missing

Ensure that `BlazorAI/mydb.db` exists and apply the migrations with `dotnet ef database update`.

## Development notes

- The project currently has no dedicated test project in the solution.
- The OpenAI provider is selected by default in `Program.cs`.
- The email implementation is a simulation and must be replaced before using this project for real email delivery.
- API keys should be supplied through User Secrets, environment variables, or a production secret-management service.
- The default project template navigation still contains links such as Counter and Weather; the functional AI chat page is the `/` route.

## License

No license file is currently included in the repository. Add a license before distributing the project or accepting external contributions under defined terms.
