using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Web.Library.AI;
using Web.Library.Mcp;

namespace Web.Library.Tests;

public class ChatServiceTests
{
    [Fact]
    public void ChatService_CanBeCreated()
    {
        var chatClient = CreateTestChatClient();
        var mcpToolService = CreateTestMcpToolService();
        var logger = CreateTestLogger();
        
        var service = new ChatService(chatClient, mcpToolService, logger);
        
        Assert.NotNull(service);
        Assert.Empty(service.Messages);
    }

    [Fact]
    public async Task ChatService_Initialization_AddsSystemMessage()
    {
        var chatClient = CreateTestChatClient();
        var mcpToolService = CreateTestMcpToolService();
        var logger = CreateTestLogger();
        var service = new ChatService(chatClient, mcpToolService, logger);
        
        await service.InitializeAsync();
        
        Assert.Single(service.Messages);
    }

    [Fact]
    public async Task ChatService_ChatAsync_AddsUserAndAssistantMessages()
    {
        var chatClient = CreateTestChatClient();
        var mcpToolService = CreateTestMcpToolService();
        var logger = CreateTestLogger();
        var service = new ChatService(chatClient, mcpToolService, logger);
        
        await service.InitializeAsync();
        var result = await service.ChatAsync("Hello");
        
        Assert.NotNull(result);
        Assert.Equal("Echo: Hello", result);
        Assert.Equal(3, service.Messages.Count);
    }

    private static IChatClient CreateTestChatClient()
    {
        return new TestChatClient();
    }

    private static McpToolService CreateTestMcpToolService()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
        return new McpToolService("http://localhost:5000/mcp", loggerFactory);
    }

    private static ILogger<ChatService> CreateTestLogger()
    {
        return Microsoft.Extensions.Logging.LoggerFactory.Create(b => { }).CreateLogger<ChatService>();
    }

    private class TestChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("test", null, "test-model");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var lastMessage = chatMessages.LastOrDefault()?.Text ?? "Hello";
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Echo: {lastMessage}"));
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public object? GetService(Type serviceType, object? key = null)
        {
            return null;
        }

        public void Dispose() { }
    }
}
