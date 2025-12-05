using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Web.Library.Data;
using Web.Library.Models;
using Web.Library.Mcp;
using Web.Library.Repositories;

namespace Web.Library.AI;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string mcpEndpointUrl,
        string dataApiEndpointUrl)
    {
        var connectionString = configuration.GetConnectionString("chat")
            ?? throw new InvalidOperationException("Connection string 'chat' not found.");

        var azureOpenAiOptions = AzureOpenAiConnectionOptions.FromConnectionString(connectionString);

        services.AddSingleton(azureOpenAiOptions);

        services.AddSingleton<ChatClient>(sp =>
        {
            var options = sp.GetRequiredService<AzureOpenAiConnectionOptions>();
            var credential = new AzureKeyCredential(options.Key);
            var azureClient = new AzureOpenAIClient(new Uri(options.Endpoint), credential);
            return azureClient.GetChatClient(options.Deployment);
        });

        services.AddScoped<IChatClient>(sp =>
        {
            var chatClient = sp.GetRequiredService<ChatClient>();
            var logger = sp.GetRequiredService<ILogger<AzureOpenAiChatClientAdapter>>();
            var options = sp.GetRequiredService<AzureOpenAiConnectionOptions>();
            return new AzureOpenAiChatClientAdapter(chatClient, logger, options.Deployment);
        });

        services.AddSingleton(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new McpToolService(mcpEndpointUrl, loggerFactory);
        });

        services.AddSingleton(new DataApiOptions(dataApiEndpointUrl));
        services.AddSingleton<IDataApiTableFactory, DataApiTableFactory>();
        services.AddScoped<IDataApiTable<Todo>>(sp => sp.GetRequiredService<IDataApiTableFactory>().Create<Todo>());
        services.AddScoped<IDataApiTable<Category>>(sp => sp.GetRequiredService<IDataApiTableFactory>().Create<Category>());
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ChatService>();

        return services;
    }
}
