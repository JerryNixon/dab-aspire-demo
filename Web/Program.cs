using Web.Library.AI;
using Web.Library.Diagnostics;
using Web.Library.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddWebTelemetry(builder.Configuration, builder.Environment);
builder.Logging.AddConsole();

var dataApiEndpoint = builder.Configuration["services__data-api__http__0"]
    ?? Environment.GetEnvironmentVariable("services__data-api__http__0")
    ?? "http://localhost:5000";

var mcpEndpoint = dataApiEndpoint.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase)
    ? dataApiEndpoint
    : $"{dataApiEndpoint.TrimEnd('/')}/mcp";

var chatEndpoint = builder.Configuration["ConnectionStrings:chat:Endpoint"];
var chatDeployment = builder.Configuration["ConnectionStrings:chat:Deployment"];
var chatKey = builder.Configuration["ConnectionStrings:chat:Key"];
var chatApiVersion = builder.Configuration["ConnectionStrings:chat:ApiVersion"];
var chatConnectionMissing = false;

if (!string.IsNullOrWhiteSpace(chatEndpoint) &&
    !string.IsNullOrWhiteSpace(chatDeployment) &&
    !string.IsNullOrWhiteSpace(chatKey))
{
    var chatConn = $"Endpoint={chatEndpoint};Deployment={chatDeployment};Key={chatKey};ApiVersion={chatApiVersion}";
    builder.Configuration["ConnectionStrings:chat"] = chatConn;
}
else
{
    builder.Configuration["ConnectionStrings:chat"] = string.Empty;
    chatConnectionMissing = true;
}

builder.Services.AddAiServices(builder.Configuration, mcpEndpoint, dataApiEndpoint);

var app = builder.Build();

if (chatConnectionMissing)
{
    app.Logger.LogWarning("Azure AI Foundry connection string is incomplete. Ensure all required parameters are provided.");
}

app.UseStaticFiles();

app.MapRazorPages();

app.MapChat();

app.Run();
