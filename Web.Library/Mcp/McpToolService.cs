using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Web.Library.Diagnostics;

namespace Web.Library.Mcp;

public sealed class McpToolService
{
    private readonly string _mcpEndpoint;
    private readonly ILogger<McpToolService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private List<McpClientTool>? _tools;
    private IMcpClient? _mcpClient;

    public McpToolService(string mcpEndpoint, ILoggerFactory loggerFactory)
    {
        _mcpEndpoint = mcpEndpoint;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpToolService>();
    }

    private async Task<IMcpClient> GetOrCreateClientAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.StartActivity("mcp.client.ensure", ActivityKind.Client);
        activity?.SetTag("mcp.endpoint", _mcpEndpoint);
        if (_mcpClient is not null)
        {
            activity?.AddEvent(new ActivityEvent("mcp.client.cached"));
            return _mcpClient;
        }

        _logger.LogInformation("Creating MCP client for {Endpoint}", _mcpEndpoint);

        _mcpClient = await McpClientFactory.CreateAsync(
            new SseClientTransport(new()
            {
                Endpoint = new Uri(_mcpEndpoint),
                Name = "dab-mcp-client"
            }),
            loggerFactory: _loggerFactory,
            cancellationToken: cancellationToken);

        activity?.AddEvent(new ActivityEvent("mcp.client.created"));

        return _mcpClient;
    }

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.StartActivity("mcp.tools.fetch", ActivityKind.Client);
        activity?.SetTag("mcp.endpoint", _mcpEndpoint);
        if (_tools is not null)
        {
            activity?.AddEvent(new ActivityEvent("mcp.tools.cached"));
            return _tools.Cast<AITool>().ToList();
        }

        try
        {
            _logger.LogInformation("Fetching MCP tools from {Endpoint}", _mcpEndpoint);

            var client = await GetOrCreateClientAsync(cancellationToken);
            _tools = (await client.ListToolsAsync()).ToList();

            activity?.SetTag("mcp.tool.count", _tools.Count);
            activity?.AddEvent(new ActivityEvent("mcp.tools.received"));

            _logger.LogInformation("Registered {Count} MCP tools: {Tools}",
                _tools.Count, string.Join(", ", _tools.Select(t => t.Name)));

            return _tools.Cast<AITool>().ToList();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to fetch MCP tools from {Endpoint}", _mcpEndpoint);
            _tools = new List<McpClientTool>();
            return _tools.Cast<AITool>().ToList();
        }
    }

    public async Task<string> ExecuteToolAsync(
        string toolName,
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.StartActivity("mcp.tool.call", ActivityKind.Client);
        activity?.SetTag("mcp.endpoint", _mcpEndpoint);
        activity?.SetTag("mcp.tool.name", toolName);
        try
        {
            _logger.LogInformation("Executing tool {Tool} with arguments: {Args}",
                toolName, JsonSerializer.Serialize(arguments));

            var client = await GetOrCreateClientAsync(cancellationToken);

            var readonlyArgs = arguments as IReadOnlyDictionary<string, object?>
                ?? new Dictionary<string, object?>(arguments);

            var result = await client.CallToolAsync(toolName, readonlyArgs);

            var resultJson = JsonSerializer.Serialize(result);
            activity?.SetTag("mcp.tool.result.length", resultJson.Length);
            _logger.LogDebug("Tool {Tool} returned: {Result}", toolName, resultJson);

            return resultJson;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error executing tool {Tool}", toolName);
            throw;
        }
    }
}
