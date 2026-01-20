# Claude Agent SDK for .NET - API Documentation

## Namespaces

- `ClaudeAgentSDK` - Main SDK namespace
- `ClaudeAgentSDK.Models` - Type definitions
- `ClaudeAgentSDK.Internal` - Internal implementation (not for public use)
- `ClaudeAgentSDK.Internal.Transport` - Transport layer

## ClaudeAgent Class

Static entry point for one-shot queries.

### Methods

#### QueryAsync

```csharp
public static IAsyncEnumerable<IMessage> QueryAsync(
    string prompt,
    ClaudeAgentOptions? options = null,
    ITransport? transport = null,
    ILogger? logger = null,
    CancellationToken cancellationToken = default)
```

Sends a one-shot query and returns messages as an async stream.

**Parameters:**
- `prompt`: The prompt to send to Claude
- `options`: Optional configuration options
- `transport`: Optional custom transport implementation
- `logger`: Optional ILogger for diagnostics
- `cancellationToken`: Cancellation token

**Returns:** `IAsyncEnumerable<IMessage>` - Stream of messages

---

#### QueryTextAsync

```csharp
public static Task<string?> QueryTextAsync(
    string prompt,
    ClaudeAgentOptions? options = null,
    CancellationToken cancellationToken = default)
```

Sends a query and returns the final result text.

---

#### QueryToListAsync

```csharp
public static Task<List<IMessage>> QueryToListAsync(
    string prompt,
    ClaudeAgentOptions? options = null,
    CancellationToken cancellationToken = default)
```

Sends a query and collects all messages into a list.

---

#### CreateClient

```csharp
public static ClaudeSDKClient CreateClient(
    ClaudeAgentOptions? options = null,
    ITransport? transport = null,
    ILogger? logger = null)
```

Creates a new interactive client for bidirectional communication.

---

## ClaudeSDKClient Class

Interactive client for bidirectional communication with Claude Code CLI.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Whether the client is connected |
| `ServerInfo` | `Dictionary<string, object?>?` | Server initialization info |

### Methods

#### ConnectAsync

```csharp
public Task ConnectAsync(
    string? prompt = null,
    CancellationToken cancellationToken = default)
```

Connects to the CLI and optionally starts with an initial prompt.

---

#### QueryAsync

```csharp
public Task QueryAsync(
    string prompt,
    string? sessionId = null,
    CancellationToken cancellationToken = default)
```

Sends a new query in the current session.

---

#### ReceiveMessagesAsync

```csharp
public IAsyncEnumerable<IMessage> ReceiveMessagesAsync(
    CancellationToken cancellationToken = default)
```

Receives all messages from the CLI as an async stream.

---

#### ReceiveResponseAsync

```csharp
public IAsyncEnumerable<IMessage> ReceiveResponseAsync(
    CancellationToken cancellationToken = default)
```

Receives messages until a ResultMessage is received.

---

#### InterruptAsync

```csharp
public Task InterruptAsync(CancellationToken cancellationToken = default)
```

Sends an interrupt signal to stop the current operation.

---

#### SetPermissionModeAsync

```csharp
public Task SetPermissionModeAsync(
    PermissionMode mode,
    CancellationToken cancellationToken = default)
```

Changes the permission mode during the conversation.

---

#### SetModelAsync

```csharp
public Task SetModelAsync(
    string? model,
    CancellationToken cancellationToken = default)
```

Changes the AI model during the conversation.

---

#### RewindFilesAsync

```csharp
public Task RewindFilesAsync(
    string userMessageId,
    CancellationToken cancellationToken = default)
```

Rewinds files to a specific checkpoint.

---

#### DisconnectAsync

```csharp
public Task DisconnectAsync(CancellationToken cancellationToken = default)
```

Disconnects from the CLI.

---

## Models

### IMessage Interface

Base interface for all message types.

```csharp
public interface IMessage
{
    string Type { get; }
}
```

### UserMessage

```csharp
public sealed record UserMessage : IMessage
{
    public string Type => "user";
    public required object Content { get; init; }
    public string? Uuid { get; init; }
    public string? ParentToolUseId { get; init; }

    public string? GetContentAsString();
    public IReadOnlyList<IContentBlock>? GetContentBlocks();
}
```

### AssistantMessage

```csharp
public sealed record AssistantMessage : IMessage
{
    public string Type => "assistant";
    public required IReadOnlyList<IContentBlock> Content { get; init; }
    public required string Model { get; init; }
    public string? ParentToolUseId { get; init; }
    public AssistantMessageError? Error { get; init; }
}
```

### SystemMessage

```csharp
public sealed record SystemMessage : IMessage
{
    public string Type => "system";
    public required string Subtype { get; init; }
    public Dictionary<string, object?>? Data { get; init; }
}
```

### ResultMessage

```csharp
public sealed record ResultMessage : IMessage
{
    public string Type => "result";
    public required string Subtype { get; init; }
    public required int DurationMs { get; init; }
    public required int DurationApiMs { get; init; }
    public required bool IsError { get; init; }
    public required int NumTurns { get; init; }
    public required string SessionId { get; init; }
    public double? TotalCostUsd { get; init; }
    public Dictionary<string, object?>? Usage { get; init; }
    public string? Result { get; init; }
    public object? StructuredOutput { get; init; }
}
```

### StreamEvent

```csharp
public sealed record StreamEvent : IMessage
{
    public string Type => "stream_event";
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public required Dictionary<string, object?> Event { get; init; }
    public string? ParentToolUseId { get; init; }
}
```

---

## Content Blocks

### IContentBlock Interface

```csharp
public interface IContentBlock
{
    string Type { get; }
}
```

### TextBlock

```csharp
public sealed record TextBlock : IContentBlock
{
    public string Type => "text";
    public required string Text { get; init; }
}
```

### ThinkingBlock

```csharp
public sealed record ThinkingBlock : IContentBlock
{
    public string Type => "thinking";
    public required string Thinking { get; init; }
    public required string Signature { get; init; }
}
```

### ToolUseBlock

```csharp
public sealed record ToolUseBlock : IContentBlock
{
    public string Type => "tool_use";
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Dictionary<string, object?> Input { get; init; }
}
```

### ToolResultBlock

```csharp
public sealed record ToolResultBlock : IContentBlock
{
    public string Type => "tool_result";
    public required string ToolUseId { get; init; }
    public object? Content { get; init; }
    public bool? IsError { get; init; }
}
```

---

## Options

### ClaudeAgentOptions

```csharp
public sealed class ClaudeAgentOptions
{
    // Tool configuration
    public List<string>? Tools { get; set; }
    public ToolsPreset? ToolsPreset { get; set; }
    public List<string> AllowedTools { get; set; }
    public List<string> DisallowedTools { get; set; }

    // System prompt
    public string? SystemPrompt { get; set; }

    // Model selection
    public string? Model { get; set; }
    public string? FallbackModel { get; set; }

    // MCP servers
    public Dictionary<string, McpServerConfig>? McpServers { get; set; }
    public string? McpConfigPath { get; set; }

    // Permission control
    public PermissionMode? PermissionMode { get; set; }
    public string? PermissionPromptToolName { get; set; }
    public Func<ToolPermissionRequest, Task<PermissionResult>>? CanUseTool { get; set; }

    // Session control
    public bool ContinueConversation { get; set; }
    public string? Resume { get; set; }
    public int? MaxTurns { get; set; }
    public double? MaxBudgetUsd { get; set; }
    public bool ForkSession { get; set; }

    // Working directory
    public string? WorkingDirectory { get; set; }

    // CLI configuration
    public string? CliPath { get; set; }
    public string? Settings { get; set; }
    public List<string> AddDirs { get; set; }
    public Dictionary<string, string> Environment { get; set; }

    // Advanced options
    public bool IncludePartialMessages { get; set; }
    public int? MaxBufferSize { get; set; }
    public Action<string>? StderrCallback { get; set; }

    // Hooks
    public Dictionary<HookEvent, List<HookMatcher>>? Hooks { get; set; }

    // Beta features
    public List<string> Betas { get; set; }

    // Agents
    public Dictionary<string, AgentDefinition>? Agents { get; set; }

    // Plugins
    public List<PluginConfig> Plugins { get; set; }

    // Sandbox
    public SandboxSettings? Sandbox { get; set; }

    // Output formatting
    public int? MaxThinkingTokens { get; set; }
    public Dictionary<string, object?>? OutputFormat { get; set; }

    // File checkpointing
    public bool EnableFileCheckpointing { get; set; }
}
```

---

## Enums

### PermissionMode

```csharp
public enum PermissionMode
{
    Default,
    AcceptEdits,
    Plan,
    BypassPermissions
}
```

### ToolsPreset

```csharp
public enum ToolsPreset
{
    All,
    ReadOnly,
    None
}
```

### HookEvent

```csharp
public enum HookEvent
{
    PreToolUse,
    PostToolUse,
    UserPromptSubmit,
    Stop,
    SubagentStop,
    PreCompact
}
```

---

## Exceptions

| Exception | Description |
|-----------|-------------|
| `ClaudeSDKException` | Base exception for all SDK errors |
| `CliConnectionException` | Unable to connect to CLI |
| `CliNotFoundException` | CLI not found or not installed |
| `ProcessException` | CLI process failed |
| `CliJsonDecodeException` | Unable to decode JSON from CLI |
| `MessageParseException` | Unable to parse a message |
| `ControlRequestTimeoutException` | Control request timed out |
| `NotConnectedException` | Client is not connected |
