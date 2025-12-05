# Chat Implementation with Ollama & MCP

This document describes the AI chat integration added to the dab-aspire-demo solution.

## Overview

The solution now includes an AI-powered chat assistant that can help users manage their todo items through natural language. The chat interface is accessible via a floating widget in the bottom-right corner of the web application.

## Architecture

### Components

1. **Web.Library** - Reusable class library containing:
   - ChatService - Handles chat interactions with Ollama
   - ChatConfig - Manages configuration and environment variables

2. **Web.Library.Tests** - Unit tests for the chat functionality

3. **AppHost** - Aspire orchestration with added Ollama container

4. **Web** - Web application with:
   - Chat API endpoint (/api/chat)
   - Floating chat widget UI
   - Styled chat interface matching the existing design

### Technology Stack

- **LLM**: Ollama with Phi-3 Mini model
- **Chat Library**: OllamaSharp (v5.4.8)
- **AI Abstractions**: Microsoft.Extensions.AI
- **MCP Client**: MCPSharp (ready for integration)
- **Testing**: xUnit

## Key Features

### 1. Isolated Service Architecture

The ChatService is fully self-contained in Web.Library, making it easy to reuse in other projects:

\\\csharp
public class ChatService
{
    public async Task InitializeAsync()
    public async Task<string> ChatAsync(string userMessage)
    public void ClearHistory()
}
\\\

### 2. Environment-Based Configuration

\\\csharp
public class ChatConfig
{
    public string OllamaEndpoint    // services__ollama__http__0
    public string OllamaModel       // OLLAMA_MODEL or phi3:mini
    public string McpEndpoint       // services__data-api__https__0
}
\\\

### 3. Clean Chat UI

- Floating button in bottom-right corner
- Expandable chat window
- Message history with user/assistant distinction
- Typing indicators
- Mobile-responsive design

## Setup Instructions

### 1. Run the Application

\\\ash
dotnet run --project AppHost
\\\

### 2. Access the Chat

1. Navigate to the web application (https://localhost:XXXX)
2. Click the chat button in the bottom-right corner
3. Type a message and press Send

### 3. First Run - Model Download

On first use, Ollama will download the Phi-3 Mini model (~2.3GB). This may take several minutes depending on your connection.

## How It Works

### Chat Flow

1. User types a message in the chat widget
2. JavaScript sends POST request to /api/chat
3. ChatService processes the message:
   - Maintains conversation history
   - Calls Ollama API with full context
   - Streams response back
4. Response displayed in chat UI

### Ollama Integration

\\\csharp
var client = new OllamaApiClient(endpoint, model);
var chat = new Chat(client);
var response = await chat.SendAsync(message);
\\\

### MCP Integration (Planned)

The architecture supports MCP tool calling:

1. ChatService will load tools from DAB's MCP endpoint
2. Ollama model will be able to call these tools
3. Tools can modify todos in the database
4. UI can be refreshed to show changes

## File Structure

\\\
Web.Library/
 ChatService.cs       # Main chat orchestration
 ChatConfig.cs        # Configuration management
 Web.Library.csproj   # Package references

Web.Library.Tests/
 ChatServiceTests.cs  # Unit tests (3 tests passing)

Web/
 Program.cs           # Service registration & API endpoint
 Pages/
    Index.cshtml     # Chat widget HTML & JS
 wwwroot/css/
     index.css        # Chat widget styles

AppHost/
 AppHost.cs           # Ollama container configuration
\\\

## Key Design Decisions

### 1. Simplicity Over Abstraction

- Direct use of OllamaSharp for clarity
- Minimal layers between UI and LLM
- Straightforward error handling

### 2. Reusability

- Web.Library is self-contained
- No dependencies on the Web project
- Easy to copy to other solutions

### 3. Maintainability

- Small, focused classes
- Clear method names
- Comprehensive logging
- Well-structured tests

### 4. Developer Experience

- Environment variable auto-discovery
- Graceful fallbacks
- Clear error messages
- TypeScript-free (pure JS)

## Testing

Run the tests:

\\\ash
dotnet test
\\\

Current test coverage:
-  ChatService creation
-  Initialization adds system message
-  ChatAsync adds user and assistant messages

## Future Enhancements

1. **MCP Tool Integration**
   - Load tools from DAB endpoint
   - Enable function calling
   - Auto-refresh UI on data changes

2. **Markdown Rendering**
   - Support basic formatting (bold, italic, lists)
   - Code block highlighting

3. **Chat History Persistence**
   - Save conversation per session
   - Allow multiple conversations

4. **Streaming UI**
   - Show tokens as they arrive
   - Smoother user experience

## Troubleshooting

### Ollama Container Not Starting

Check if port 11434 is available:
\\\ash
netstat -ano | findstr :11434
\\\

### Chat Returns Errors

1. Check Ollama container logs in Aspire dashboard
2. Verify model is downloaded: Visit http://localhost:11434/api/tags
3. Check Web application logs for detailed errors

### Model Too Slow

Consider using a smaller model:
\\\csharp
Ollama = new { ResourceName = "ollama", Model = "phi3:mini" }
\\\

Or a larger one if you have the resources:
\\\csharp
Ollama = new { ResourceName = "ollama", Model = "llama3.2:3b" }
\\\

## Resources

- [OllamaSharp Documentation](https://github.com/awaescher/OllamaSharp)
- [Ollama Model Library](https://ollama.ai/library)
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [MCPSharp](https://www.nuget.org/packages/MCPSharp)

## License

This implementation follows the same license as the parent project.
