using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Web.Library.AI;

namespace Web.Library.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChat(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", async (HttpContext context, ChatService chatService, ILogger<ChatService> logger) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ChatRequest>();

            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest("Message is required");
            }

            try
            {
                if (!chatService.Messages.Any())
                {
                    await chatService.InitializeAsync();
                }

                var result = await chatService.ChatAsync(request.Message);
                return Results.Ok(new { response = result });
            }
            catch (Azure.RequestFailedException requestFailedEx)
            {
                logger.LogError(requestFailedEx, "Azure OpenAI request failed");
                var status = requestFailedEx.Status >= 400 ? requestFailedEx.Status : 502;
                return Results.Json(new
                {
                    error = true,
                    message = "Azure OpenAI rejected the request",
                    details = "Verify the deployment name, API version, and request parameters.",
                    technical = requestFailedEx.Message
                }, statusCode: status);
            }
            catch (TaskCanceledException)
            {
                logger.LogError("Request to Azure OpenAI timed out");
                return Results.Json(new
                {
                    error = true,
                    message = "Request timed out",
                    details = "Azure OpenAI took too long to respond. Try again with a shorter message or retry later.",
                    technical = "Request exceeded timeout threshold"
                }, statusCode: 504);
            }
            catch (InvalidOperationException invalidEx)
            {
                logger.LogError(invalidEx, "Azure OpenAI client configuration error");
                return Results.Json(new
                {
                    error = true,
                    message = "AI service not configured",
                    details = "Azure OpenAI configuration is missing or invalid. Confirm the connection string values in Aspire.",
                    technical = invalidEx.Message
                }, statusCode: 500);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in chat endpoint");
                return Results.Json(new
                {
                    error = true,
                    message = "An unexpected error occurred",
                    details = $"Error type: {ex.GetType().Name}. This may indicate a configuration or connectivity issue with Azure OpenAI.",
                    technical = ex.Message
                }, statusCode: 500);
            }
        });

        return endpoints;
    }

    public sealed record ChatRequest(string Message);
}