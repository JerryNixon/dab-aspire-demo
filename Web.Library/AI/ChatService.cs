using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Web.Library.Diagnostics;
using Web.Library.Mcp;

namespace Web.Library.AI;

public sealed class ChatService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatService> _logger;
    private readonly McpToolService _mcpToolService;

    private readonly List<ChatMessage> _messages = [];
    private ChatOptions? _chatOptions;

    public ChatService(IChatClient chatClient, McpToolService mcpToolService, ILogger<ChatService> logger)
    {
        _chatClient = chatClient;
        _mcpToolService = mcpToolService;
        _logger = logger;
    }

    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    public async Task InitializeAsync()
    {
    using var activity = Telemetry.StartActivity("chat.initialize");
    _logger.LogInformation("Initializing chat service");

        const string systemPrompt = """
            You are an assistant that manages todo items using MCP tools.

            MCP USAGE RULES
            1. Always call describe_entities first in a session. This is mandatory.
            It returns entity names, fields, keys, and permissions. Cache this metadata for all later tool calls.
            2. Every operation must include the entity name from describe_entities.
            Most operations also require field and key names from the cached metadata.
            3. Use read_records (with $filter) to answer reporting or lookup questions.
            4. Use create_record, update_record, delete_record, or execute_entity only when the user explicitly requests a change.
            5. Never guess table, entity, or column names. If metadata is missing, call describe_entities again.

            BE HELPFUL
            • Keep message text concise, friendly, and specific about what was done or found.
            • Acknowledge when no records match.
            • Never fabricate data.
            • Always follow the flow exactly: describe_entities first, then use the cached metadata for all tool calls.
            """;

        _messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        try
        {
            var tools = await _mcpToolService.GetToolsAsync();
            activity?.SetTag("mcp.tool.count", tools.Count);
            _chatOptions = new ChatOptions { Tools = [.. tools] };

            foreach (var tool in tools)
            {
                var toolType = tool.GetType();
                var propertyNames = string.Join(", ", toolType.GetProperties().Select(p => p.Name));
                _logger.LogInformation("Loaded tool {ToolType} with properties: {Properties}", toolType.Name, propertyNames);
            }

            _logger.LogInformation("Loaded {Count} tools from MCP server", tools.Count);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to load MCP tools, continuing without tools");
            _chatOptions = new ChatOptions();
        }
    }

    public async Task<string> ChatAsync(string userMessage)
    {
        using var activity = Telemetry.StartActivity("chat.exchange");
        activity?.SetTag("chat.message.length", userMessage.Length);
        _logger.LogInformation("Processing message: {Message}", userMessage);
        _messages.Add(new ChatMessage(ChatRole.User, userMessage));

        try
        {
            const int maxIterations = 5;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                _logger.LogDebug("Chat iteration {Iteration} of {Max}", iteration + 1, maxIterations);

                activity?.AddEvent(new ActivityEvent("chat.request"));
                var response = await _chatClient.GetResponseAsync(_messages, _chatOptions ??= new());
                var assistantMessage = ExtractAssistantMessage(response, out var usedFallback);

                if (usedFallback)
                {
                    activity?.AddEvent(new ActivityEvent("chat.fallback"));
                    var propertyNames = string.Join(", ", response.GetType().GetProperties().Select(p => p.Name));
                    _logger.LogWarning("Falling back to basic assistant message extraction. Response properties: {Properties}", propertyNames);
                }

                _logger.LogDebug(
                    "Assistant message contents: {Contents}",
                    JsonSerializer.Serialize(assistantMessage.Contents.Select(c => c.GetType().Name)));

                _messages.Add(assistantMessage);

                var functionCalls = assistantMessage.Contents.OfType<FunctionCallContent>().ToList();
                _logger.LogDebug("Detected {Count} function calls", functionCalls.Count);

                if (functionCalls.Count == 0)
                {
                    var responseText = ExtractAssistantText(assistantMessage);
                    activity?.SetTag("chat.response.length", responseText.Length);
                    _logger.LogInformation("Final AI response: {Response}", responseText);
                    return responseText;
                }

                _logger.LogInformation("Executing {Count} function calls", functionCalls.Count);

                foreach (var functionCall in functionCalls)
                {
                    using var toolActivity = Telemetry.StartActivity("chat.tool.execute", ActivityKind.Client);
                    toolActivity?.SetTag("mcp.tool.name", functionCall.Name);
                    _logger.LogDebug("Executing function: {Name} with arguments: {Args}", functionCall.Name, functionCall.Arguments);

                    try
                    {
                        var result = await _mcpToolService.ExecuteToolAsync(
                            functionCall.Name,
                            functionCall.Arguments ?? new Dictionary<string, object?>());

                        toolActivity?.SetTag("mcp.tool.result.size", result.Length);
                        var resultContent = new FunctionResultContent(functionCall.CallId, result);
                        _messages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));

                        _logger.LogDebug("Function {Name} returned: {Result}", functionCall.Name, result);
                    }
                    catch (Exception ex)
                    {
                        toolActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        _logger.LogError(ex, "Error executing function {Name}", functionCall.Name);

                        var errorContent = new FunctionResultContent(functionCall.CallId, $"Error: {ex.Message}");
                        _messages.Add(new ChatMessage(ChatRole.Tool, [errorContent]));
                    }
                }
            }

            _logger.LogWarning("Hit max iterations ({Max}) without final response", maxIterations);
            var fallbackMessage = "I encountered too many tool calls. Please try rephrasing your request.";
            activity?.SetStatus(ActivityStatusCode.Error, "Too many tool calls");
            _messages.Add(new ChatMessage(ChatRole.Assistant, fallbackMessage));
            return fallbackMessage;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error processing chat message");
            var errorMessage = $"Sorry, I encountered an error: {ex.Message}";
            _messages.Add(new ChatMessage(ChatRole.Assistant, errorMessage));
            return errorMessage;
        }
    }

    public void ClearHistory()
    {
        var systemMessage = _messages.FirstOrDefault(m => m.Role == ChatRole.System);
        _messages.Clear();

        if (systemMessage is not null)
        {
            _messages.Add(systemMessage);
        }
    }

    private static ChatMessage ExtractAssistantMessage(ChatResponse response, out bool usedFallback)
    {
        var responseType = response.GetType();

        var message = responseType.GetProperty("Message")?.GetValue(response)
            ?? responseType.GetProperty("ResponseMessage")?.GetValue(response)
            ?? responseType.GetProperty("OutputMessage")?.GetValue(response);

        if (message is ChatMessage chatMessage)
        {
            usedFallback = false;
            return chatMessage;
        }

        var messagesValue = responseType.GetProperty("Messages")?.GetValue(response);
        if (messagesValue is IEnumerable messageEnumerable)
        {
            ChatMessage? lastAssistant = null;
            foreach (var item in messageEnumerable)
            {
                if (item is ChatMessage assistant && assistant.Role == ChatRole.Assistant)
                {
                    lastAssistant = assistant;
                }
            }

            if (lastAssistant is not null)
            {
                usedFallback = false;
                return lastAssistant;
            }
        }

        usedFallback = true;
        return new ChatMessage(ChatRole.Assistant, response.Text ?? string.Empty);
    }

    private static string ExtractAssistantText(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text;
        }

        var textContent = message.Contents.OfType<TextContent>().FirstOrDefault();
        return textContent?.Text ?? string.Empty;
    }
}
