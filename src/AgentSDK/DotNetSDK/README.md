# Claude Agent SDK for .NET

Official .NET SDK for Claude Code CLI - enables programmatic interaction with Claude AI through the Claude Code command-line interface.

## Installation

```bash
dotnet add package ClaudeAgentSDK
```

### Prerequisites

- .NET 8.0 or later
- Claude Code CLI installed (`npm install -g @anthropic/claude-code`)

## Quick Start

### Simple One-Shot Query

```csharp
using ClaudeAgentSDK;
using ClaudeAgentSDK.Models;

// Send a query and stream the response
await foreach (var message in ClaudeAgent.QueryAsync("What is the meaning of life?"))
{
    if (message is AssistantMessage assistant)
    {
        foreach (var block in assistant.Content)
        {
            if (block is TextBlock text)
            {
                Console.Write(text.Text);
            }
        }
    }
}
```

### Get Just the Result Text

```csharp
var result = await ClaudeAgent.QueryTextAsync("Explain async/await in C# in one sentence");
Console.WriteLine(result);
```

### Interactive Client (Multi-Turn Conversation)

```csharp
await using var client = ClaudeAgent.CreateClient();
await client.ConnectAsync("Hello! My name is Alice.");

// Receive first response
await foreach (var message in client.ReceiveResponseAsync())
{
    // Process messages...
}

// Continue the conversation
await client.QueryAsync("What's my name?");
await foreach (var message in client.ReceiveResponseAsync())
{
    // Claude remembers: "Your name is Alice!"
}
```

## Configuration

### ClaudeAgentOptions

```csharp
var options = new ClaudeAgentOptions
{
    // Model selection
    Model = "claude-sonnet-4-20250514",
    FallbackModel = "claude-haiku-4-20250514",

    // Tool configuration
    Tools = ["Read", "Write", "Bash"],
    AllowedTools = ["specific_tool"],
    DisallowedTools = ["dangerous_tool"],

    // Permission control
    PermissionMode = PermissionMode.AcceptEdits,

    // Limits
    MaxTurns = 10,
    MaxBudgetUsd = 1.0,
    MaxThinkingTokens = 10000,

    // Session control
    ContinueConversation = true,
    Resume = "previous-session-id",

    // Working directory
    WorkingDirectory = "/path/to/project",

    // Environment variables
    Environment = new Dictionary<string, string>
    {
        ["MY_VAR"] = "value"
    }
};

await foreach (var msg in ClaudeAgent.QueryAsync("Build the project", options))
{
    // ...
}
```

### Permission Modes

| Mode | Description |
|------|-------------|
| `Default` | Prompts for potentially dangerous operations |
| `AcceptEdits` | Automatically accepts file edits |
| `Plan` | Shows proposed changes without executing |
| `BypassPermissions` | Bypasses all permission checks (use with caution) |

## Message Types

### AssistantMessage

Contains Claude's response with content blocks:

```csharp
if (message is AssistantMessage assistant)
{
    foreach (var block in assistant.Content)
    {
        switch (block)
        {
            case TextBlock text:
                Console.WriteLine(text.Text);
                break;
            case ThinkingBlock thinking:
                Console.WriteLine($"Thinking: {thinking.Thinking}");
                break;
            case ToolUseBlock tool:
                Console.WriteLine($"Using tool: {tool.Name}");
                break;
        }
    }
}
```

### ResultMessage

Contains completion information:

```csharp
if (message is ResultMessage result)
{
    Console.WriteLine($"Session: {result.SessionId}");
    Console.WriteLine($"Turns: {result.NumTurns}");
    Console.WriteLine($"Cost: ${result.TotalCostUsd:F4}");
    Console.WriteLine($"Duration: {result.DurationMs}ms");
}
```

### UserMessage

Tool results appear as UserMessages:

```csharp
if (message is UserMessage user)
{
    var blocks = user.GetContentBlocks();
    if (blocks != null)
    {
        foreach (var block in blocks)
        {
            if (block is ToolResultBlock result)
            {
                Console.WriteLine($"Tool {result.ToolUseId}: {(result.IsError == true ? "Failed" : "Success")}");
            }
        }
    }
}
```

## Advanced Features

### Custom Permission Callback

Control tool permissions programmatically:

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = async (request) =>
    {
        Console.WriteLine($"Tool requested: {request.ToolName}");

        // Deny write operations
        if (request.ToolName.Contains("Write"))
        {
            return new PermissionResultDeny
            {
                Message = "Write operations not allowed"
            };
        }

        // Allow with modified input
        return new PermissionResultAllow
        {
            UpdatedInput = request.Input
        };
    }
};
```

### Hooks

Register callbacks for various events:

```csharp
var options = new ClaudeAgentOptions
{
    Hooks = new Dictionary<HookEvent, List<HookMatcher>>
    {
        [HookEvent.PreToolUse] = new List<HookMatcher>
        {
            new HookMatcher
            {
                Matcher = "Bash",
                Hooks = new List<HookCallback>
                {
                    new HookCallback
                    {
                        Handler = async (input, matcher, context) =>
                        {
                            if (input is PreToolUseHookInput preToolUse)
                            {
                                Console.WriteLine($"About to run Bash command");
                            }
                            return new HookOutput { Continue = true };
                        }
                    }
                }
            }
        }
    }
};
```

### Interrupt Operations

```csharp
await using var client = ClaudeAgent.CreateClient();
await client.ConnectAsync("Do a long-running task");

// Start receiving in background
var receiveTask = Task.Run(async () =>
{
    await foreach (var msg in client.ReceiveMessagesAsync())
    {
        // Process...
    }
});

// Interrupt after 5 seconds
await Task.Delay(5000);
await client.InterruptAsync();
```

### Change Model Mid-Conversation

```csharp
await client.SetModelAsync("claude-opus-4-20250514");
```

### File Checkpointing and Rewind

```csharp
var options = new ClaudeAgentOptions
{
    EnableFileCheckpointing = true
};

await using var client = ClaudeAgent.CreateClient(options);
await client.ConnectAsync("Make some changes to the code");

// After getting a message with a UUID...
await client.RewindFilesAsync("user-message-uuid");
```

## MCP Server Configuration

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["my-server"] = new McpStdioServerConfig
        {
            Command = "node",
            Args = ["server.js"],
            Env = new Dictionary<string, string>
            {
                ["NODE_ENV"] = "production"
            }
        }
    }
};
```

Or use a configuration file:

```csharp
var options = new ClaudeAgentOptions
{
    McpConfigPath = "/path/to/mcp-config.json"
};
```

## Error Handling

```csharp
try
{
    await foreach (var msg in ClaudeAgent.QueryAsync("Hello"))
    {
        // ...
    }
}
catch (CliNotFoundException)
{
    Console.WriteLine("Claude CLI not found. Install with: npm install -g @anthropic/claude-code");
}
catch (CliConnectionException ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (ProcessException ex)
{
    Console.WriteLine($"Process failed with exit code {ex.ExitCode}: {ex.Stderr}");
}
catch (MessageParseException ex)
{
    Console.WriteLine($"Failed to parse message: {ex.Message}");
}
```

## API Reference

### ClaudeAgent (Static)

| Method | Description |
|--------|-------------|
| `QueryAsync(prompt, options?)` | Send a query and stream responses |
| `QueryTextAsync(prompt, options?)` | Send a query and get the result text |
| `QueryToListAsync(prompt, options?)` | Send a query and collect all messages |
| `CreateClient(options?)` | Create an interactive client |

### ClaudeSDKClient

| Method | Description |
|--------|-------------|
| `ConnectAsync(prompt?)` | Connect and optionally send initial prompt |
| `QueryAsync(prompt, sessionId?)` | Send a new query |
| `ReceiveMessagesAsync()` | Receive all messages as stream |
| `ReceiveResponseAsync()` | Receive messages until ResultMessage |
| `InterruptAsync()` | Interrupt current operation |
| `SetPermissionModeAsync(mode)` | Change permission mode |
| `SetModelAsync(model)` | Change AI model |
| `RewindFilesAsync(messageId)` | Rewind files to checkpoint |
| `DisconnectAsync()` | Disconnect from CLI |

## Building from Source

```bash
cd DotNetSDK
dotnet restore
dotnet build
dotnet test
```

## License

MIT License - see LICENSE file for details.
