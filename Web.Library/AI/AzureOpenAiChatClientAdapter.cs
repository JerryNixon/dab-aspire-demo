using System.ClientModel;
using System.Collections;
using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Web.Library.AI;

public sealed class AzureOpenAiChatClientAdapter : IChatClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAiChatClientAdapter> _logger;
    private readonly string _deploymentName;

    public AzureOpenAiChatClientAdapter(ChatClient chatClient, ILogger<AzureOpenAiChatClientAdapter> logger, string deploymentName)
    {
        _chatClient = chatClient;
        _logger = logger;
        _deploymentName = deploymentName;
    }

    public ChatClientMetadata Metadata => new("AzureOpenAI", null, _deploymentName);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        var mappedMessages = chatMessages.Select(ConvertMessage).ToList();
        var completionOptions = new ChatCompletionOptions();

        var maxTokens = options?.MaxOutputTokens ?? 2048;
        if (maxTokens > 0)
        {
            completionOptions.MaxOutputTokenCount = maxTokens;
        }

#pragma warning disable AOAI001
        completionOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
#pragma warning restore AOAI001

        if (options?.Tools is { Count: > 0 })
        {
            foreach (var tool in options.Tools)
            {
                var chatTool = TryCreateChatTool(tool);
                if (chatTool is ChatTool convertedTool)
                {
                    completionOptions.Tools.Add(convertedTool);
                }
                else
                {
                    _logger.LogWarning("Unable to convert tool of type {ToolType} for Azure OpenAI", tool.GetType().FullName);
                }
            }
        }

        var response = await _chatClient.CompleteChatAsync(mappedMessages, completionOptions, cancellationToken);
        LogCompletion(response.Value);
        var assistantMessage = ConvertToChatMessage(response.Value);

        if (assistantMessage.Contents.Count == 0)
        {
            _logger.LogWarning("Azure OpenAI returned no content; defaulting to empty response.");
            assistantMessage = new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, string.Empty);
        }

        return new ChatResponse(assistantMessage);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose()
    {
    }

    private static OpenAI.Chat.ChatMessage ConvertMessage(Microsoft.Extensions.AI.ChatMessage message)
    {
        if (message.Role == ChatRole.System)
        {
            return new SystemChatMessage(message.Text ?? string.Empty);
        }

        if (message.Role == ChatRole.User)
        {
            return new UserChatMessage(message.Text ?? string.Empty);
        }

        if (message.Role == ChatRole.Assistant)
        {
            var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();

            if (functionCalls.Count > 0)
            {
                var assistantMessage = new AssistantChatMessage(message.Text ?? string.Empty);
                foreach (var call in functionCalls)
                {
                    assistantMessage.ToolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                        id: call.CallId,
                        functionName: call.Name,
                        functionArguments: BinaryData.FromObjectAsJson(call.Arguments)));
                }

                return assistantMessage;
            }

            return new AssistantChatMessage(message.Text ?? string.Empty);
        }

        if (message.Role == ChatRole.Tool)
        {
            var functionResult = message.Contents.OfType<FunctionResultContent>().FirstOrDefault();
            if (functionResult is not null)
            {
                return new ToolChatMessage(functionResult.CallId, functionResult.Result?.ToString() ?? string.Empty);
            }

            return new ToolChatMessage("unknown", message.Text ?? string.Empty);
        }

        throw new NotSupportedException($"Chat role '{message.Role}' is not supported.");
    }

    private Microsoft.Extensions.AI.ChatMessage ConvertToChatMessage(object completionValue)
    {
        var completionType = completionValue.GetType();
        object? message = GetPropertyValue(completionValue, "Output")
            ?? GetPropertyValue(completionValue, "Message")
            ?? GetPropertyValue(completionValue, "Response")
            ?? completionValue;

        if (message is IEnumerable enumerable and not string)
        {
            message = enumerable.Cast<object?>().FirstOrDefault();
        }

        if (message is null)
        {
            _logger.LogWarning("Unable to locate assistant message in completion response of type {Type}", completionType.FullName);
            return new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, string.Empty);
        }

        var contents = new List<AIContent>();
        var contentValue = GetPropertyValue(message, "Content") ?? GetPropertyValue(message, "Contents");

        if (contentValue is IEnumerable contentItems)
        {
            foreach (var item in contentItems)
            {
                var text = GetPropertyValue(item!, "Text") as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    contents.Add(new TextContent(text));
                }
            }
        }
        else if (contentValue is string textValue && !string.IsNullOrWhiteSpace(textValue))
        {
            contents.Add(new TextContent(textValue));
        }

        var toolCallsValue = GetPropertyValue(message, "ToolCalls") ?? GetPropertyValue(message, "FunctionCalls");
        if (toolCallsValue is IEnumerable toolCalls)
        {
            foreach (var toolCall in toolCalls.Cast<object?>())
            {
                if (toolCall is null)
                {
                    continue;
                }

                var callId = GetPropertyValue(toolCall, "Id") as string
                    ?? GetPropertyValue(toolCall, "CallId") as string
                    ?? Guid.NewGuid().ToString();

                var functionName = GetPropertyValue(toolCall, "FunctionName") as string
                    ?? GetPropertyValue(toolCall, "Name") as string
                    ?? string.Empty;

                var rawArguments = GetPropertyValue(toolCall, "FunctionArguments")
                    ?? GetPropertyValue(toolCall, "Arguments");

                var arguments = ConvertToDictionary(rawArguments);

                contents.Add(new FunctionCallContent(callId, functionName, new Dictionary<string, object?>(arguments)));
            }
        }

        if (contents.Count == 0)
        {
            contents.Add(new TextContent(string.Empty));
        }

        return new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, contents);
    }

    private void LogCompletion(object completionValue)
    {
        try
        {
            var type = completionValue.GetType();
            var properties = string.Join(", ", type.GetProperties().Select(p => p.Name));
            _logger.LogDebug("Azure OpenAI completion type {Type} with properties: {Properties}", type.FullName, properties);

            var toolCallsProperty = type.GetProperty("ToolCalls")?.GetValue(completionValue)
                ?? type.GetProperty("FunctionCalls")?.GetValue(completionValue);

            if (toolCallsProperty is IEnumerable toolCalls)
            {
                foreach (var call in toolCalls)
                {
                    if (call is null)
                    {
                        continue;
                    }

                    var callType = call.GetType();
                    var callProps = string.Join(", ", callType.GetProperties().Select(p => p.Name));
                    _logger.LogDebug("Tool call candidate type {Type} props: {Props}", callType.FullName, callProps);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log completion metadata");
        }
    }

    private ChatTool? TryCreateChatTool(AITool tool)
    {
        var toolType = tool.GetType();
        var name = GetPropertyValue(tool, "Name") as string;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = toolType.Name;
        }

        var description = GetPropertyValue(tool, "Description") as string;
        var schemaValue = GetPropertyValue(tool, "InputSchema")
            ?? GetPropertyValue(tool, "JsonSchema")
            ?? GetPropertyValue(tool, "Schema")
            ?? GetPropertyValue(tool, "Parameters");

        var schemaBinary = ConvertToBinaryData(schemaValue) ?? BinaryData.FromString("{}");

        try
        {
            return ChatTool.CreateFunctionTool(name, description, schemaBinary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert tool {Name} to ChatTool", name);
            return null;
        }
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(propertyName);
        return property?.GetValue(instance);
    }

    private static BinaryData? ConvertToBinaryData(object? value) => value switch
    {
        null => null,
        BinaryData data => data,
        string str when !string.IsNullOrWhiteSpace(str) => BinaryData.FromString(str),
        JsonDocument doc => BinaryData.FromString(doc.RootElement.GetRawText()),
        JsonElement element => BinaryData.FromString(element.GetRawText()),
        IDictionary<string, object?> dict => BinaryData.FromString(JsonSerializer.Serialize(dict)),
        _ => BinaryData.FromString(JsonSerializer.Serialize(value))
    };

    private static IReadOnlyDictionary<string, object?> ConvertToDictionary(object? value)
    {
        if (value is null)
        {
            return new Dictionary<string, object?>();
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDict)
        {
            return readOnlyDict;
        }

        if (value is IDictionary<string, object?> dict)
        {
            return new Dictionary<string, object?>(dict);
        }

        if (value is BinaryData binaryData)
        {
            return ConvertToDictionary(binaryData.ToString());
        }

        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(stringValue)
                    ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?> { ["value"] = stringValue };
            }
        }

        if (value is JsonDocument doc)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(doc.RootElement.GetRawText())
                ?? new Dictionary<string, object?>();
        }

        if (value is JsonElement element)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText())
                ?? new Dictionary<string, object?>();
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new Dictionary<string, object?>();
            var index = 0;
            foreach (var item in enumerable)
            {
                result[$"arg{index}"] = item;
                index++;
            }

            return result;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(value))
            ?? new Dictionary<string, object?>();
    }
}
