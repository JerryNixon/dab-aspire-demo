var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREINTERACTION001
var setup = new
{
    Sql = new
    {
        ServerName = "sql-server",
        DatabaseName = "sql-database",
        VolumeName = "sql-data-volume"
    },
    SqlProject = new
    {
        ResourceName = "sql-proj"
    },
    SqlCommander = new
    {
        ResourceName = "sql-cmdr",
        Tag = "latest"
    },
    DAB = new
    {
        ResourceName = "data-api",
        Tag = "1.7.81-rc",
        ConfigPath = "../api/dab-config.json"
    },
    Redis = new
    {
        ResourceName = "redis-cache"
    },
    RedisCommander = new
    {
        ResourceName = "redis-cmdr",
        Image = "rediscommander/redis-commander",
        Tag = "latest"
    },
    MCP = new
    {
        ResourceName = "mcp-inspector",
        NoAuth = true
    },
    AiFoundry = new
    {
        ResourceName = "chat",
        DeploymentName = "o4-mini",
        Endpoint = "https://jnixon-openai.cognitiveservices.azure.com/",
        ApiVersion = "2025-01-01-preview",
        ApiKey = builder.AddParameter("azure-aifoundry-apikey")
        .WithCustomInput(p => new()
        {
            Name = p.Name,
            Label = "Azure AI Foundry API Key",
            Placeholder = "Enter your API key",
            InputType = InputType.SecretText,
            Description = "Find your keys here: [Azure AI Foundry](https://ai.azure.com)",
            EnableDescriptionMarkdown = true
        })
    },
    Web = new
    {
        ResourceName = "web-app"
    }
};
#pragma warning restore ASPIREINTERACTION001

var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer(name: setup.Sql.ServerName, password: sqlPassword)
    //.WithLifetime(ContainerLifetime.Persistent)
    //.WithDataVolume(setup.Sql.VolumeName)
    ;
sqlPassword.WithParentRelationship(sqlServer);

var sqlDatabase = sqlServer.AddDatabase(setup.Sql.DatabaseName);
var sqlProject = builder
    .AddSqlProject<Projects.Database>(name: setup.SqlProject.ResourceName)
    .WithSkipWhenDeployed()
    .WithReference(sqlDatabase);

builder
    .AddContainer(name: setup.SqlCommander.ResourceName,
                  image: "jerrynixon/sql-commander",
                  tag: setup.SqlCommander.Tag)
    .WithImageRegistry("docker.io")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithEnvironment("ConnectionStrings__db", sqlDatabase)
    .WithHttpHealthCheck("/health")
    .WithUrls(context =>
    {
        context.Urls.Clear();
        context.Urls.Add(new() { Url = "/", DisplayText = "Commander", Endpoint = context.GetEndpoint("http") });
    })
    .WithParentRelationship(sqlDatabase)
    .WaitForCompletion(sqlProject)
    .WithExplicitStart()
    .ExcludeFromManifest();

var dabConfig = new FileInfo(setup.DAB.ConfigPath);
var dabEngine = builder
    .AddContainer(name: setup.DAB.ResourceName,
                  image: "azure-databases/data-api-builder",
                  tag: setup.DAB.Tag)
    .WithImageRegistry("mcr.microsoft.com")
    .WithBindMount(source: dabConfig.FullName, target: "/App/dab-config.json", isReadOnly: true)
    .WithHttpEndpoint(targetPort: 5000, name: "http")
    .WithHttpEndpoint(targetPort: 5001, name: "https")
    .WithUrls(context =>
    {
        context.Urls.Clear();
        context.Urls.Add(new() { Url = "/graphql", DisplayText = "Nitro", Endpoint = context.GetEndpoint("http") });
        context.Urls.Add(new() { Url = "/swagger", DisplayText = "Swagger", Endpoint = context.GetEndpoint("http") });
        context.Urls.Add(new() { Url = "/health", DisplayText = "Health", Endpoint = context.GetEndpoint("http") });
    })
    .WithOtlpExporter()
    .WithHttpHealthCheck("/health");

var redisCache = builder
    .AddRedis(setup.Redis.ResourceName)
    .WithParentRelationship(dabEngine);

redisCache.WithRedisCommander(x =>
    {
        x.WithParentRelationship(redisCache);
        x.ExcludeFromManifest();
        x.WithExplicitStart();
        x.WithUrls(context =>
         {
             context.Urls.Clear();
             context.Urls.Add(new() { Url = "/", DisplayText = "Commander", Endpoint = context.GetEndpoint("http") });
         });
    }, "redis-cmdr");

var mcpInspector = builder
    .AddMcpInspector(setup.MCP.ResourceName)
    .WithMcpServer(dabEngine)
    .WithParentRelationship(dabEngine)
    .WithEnvironment("NODE_TLS_REJECT_UNAUTHORIZED", "0")
    .WaitFor(dabEngine)
    .WithEnvironment("DAB_BASE_URL", dabEngine.GetEndpoint("http"))
    .WithUrls(context =>
    {
        context.Urls.First().DisplayText = "Inspector";
    })
    .ExcludeFromManifest()
    .WithExplicitStart();

_ = dabEngine
    .WithEnvironment("ConnectionStrings__db", sqlDatabase)
    .WithEnvironment("ConnectionStrings__redis", redisCache)
    .WaitForCompletion(sqlProject);

var webApp = builder
    .AddProject<Projects.Web>(name: setup.Web.ResourceName)
    .WithReference(dabEngine.GetEndpoint("http"))
    .WithEnvironment("ConnectionStrings__chat__Endpoint", setup.AiFoundry.Endpoint)
    .WithEnvironment("ConnectionStrings__chat__Deployment", setup.AiFoundry.DeploymentName)
    .WithEnvironment("ConnectionStrings__chat__ApiVersion", setup.AiFoundry.ApiVersion)
    .WithEnvironment("ConnectionStrings__chat__Key", setup.AiFoundry.ApiKey)
    .WaitFor(dabEngine);

var otlpEndpoint = builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    webApp.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);
}

webApp.WithUrls(context =>
{
    context.Urls.Clear();
    context.Urls.Add(new() { Url = "/", DisplayText = "Web site", Endpoint = context.GetEndpoint("https") });
});

builder.Build().Run();
