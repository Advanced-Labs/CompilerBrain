using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Permission modes for tool execution.
/// </summary>
public enum PermissionMode
{
    /// <summary>
    /// Default permission mode - prompts for potentially dangerous operations.
    /// </summary>
    Default,

    /// <summary>
    /// Automatically accepts file edits.
    /// </summary>
    AcceptEdits,

    /// <summary>
    /// Plan mode - shows proposed changes without executing.
    /// </summary>
    Plan,

    /// <summary>
    /// Bypasses all permission checks (use with caution).
    /// </summary>
    BypassPermissions
}

/// <summary>
/// Tool preset configurations.
/// </summary>
public enum ToolsPreset
{
    /// <summary>
    /// All available tools.
    /// </summary>
    All,

    /// <summary>
    /// Read-only tools (no file modifications).
    /// </summary>
    ReadOnly,

    /// <summary>
    /// No tools enabled.
    /// </summary>
    None
}

/// <summary>
/// System prompt preset configurations.
/// </summary>
public enum SystemPromptPreset
{
    /// <summary>
    /// Default system prompt.
    /// </summary>
    Default
}

/// <summary>
/// Configuration options for the Claude Agent SDK.
/// </summary>
public sealed class ClaudeAgentOptions
{
    /// <summary>
    /// List of tools to enable, or a preset.
    /// </summary>
    public List<string>? Tools { get; set; }

    /// <summary>
    /// Tools preset to use.
    /// </summary>
    public ToolsPreset? ToolsPreset { get; set; }

    /// <summary>
    /// List of tools to explicitly allow.
    /// </summary>
    public List<string> AllowedTools { get; set; } = [];

    /// <summary>
    /// List of tools to explicitly disallow.
    /// </summary>
    public List<string> DisallowedTools { get; set; } = [];

    /// <summary>
    /// Custom system prompt to use.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// System prompt preset to use.
    /// </summary>
    public SystemPromptPreset? SystemPromptPreset { get; set; }

    /// <summary>
    /// Model to use for inference.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Fallback model if primary model is unavailable.
    /// </summary>
    public string? FallbackModel { get; set; }

    /// <summary>
    /// MCP server configurations. Can be a dictionary, JSON string, or file path.
    /// </summary>
    public Dictionary<string, McpServerConfig>? McpServers { get; set; }

    /// <summary>
    /// Path to MCP configuration file.
    /// </summary>
    public string? McpConfigPath { get; set; }

    /// <summary>
    /// Permission mode for tool execution.
    /// </summary>
    public PermissionMode? PermissionMode { get; set; }

    /// <summary>
    /// Tool name to use for permission prompts.
    /// </summary>
    public string? PermissionPromptToolName { get; set; }

    /// <summary>
    /// Custom permission callback function.
    /// </summary>
    public Func<ToolPermissionRequest, Task<PermissionResult>>? CanUseTool { get; set; }

    /// <summary>
    /// Whether to continue from a previous conversation.
    /// </summary>
    public bool ContinueConversation { get; set; }

    /// <summary>
    /// Session ID to resume.
    /// </summary>
    public string? Resume { get; set; }

    /// <summary>
    /// Maximum number of turns allowed.
    /// </summary>
    public int? MaxTurns { get; set; }

    /// <summary>
    /// Maximum budget in USD.
    /// </summary>
    public double? MaxBudgetUsd { get; set; }

    /// <summary>
    /// Whether to fork the session on resume.
    /// </summary>
    public bool ForkSession { get; set; }

    /// <summary>
    /// Working directory for the CLI.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Path to the Claude CLI executable.
    /// </summary>
    public string? CliPath { get; set; }

    /// <summary>
    /// Settings JSON or path to settings file.
    /// </summary>
    public string? Settings { get; set; }

    /// <summary>
    /// Additional directories to add to the context.
    /// </summary>
    public List<string> AddDirs { get; set; } = [];

    /// <summary>
    /// Environment variables to set for the CLI process.
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = [];

    /// <summary>
    /// Whether to include partial messages in the stream.
    /// </summary>
    public bool IncludePartialMessages { get; set; }

    /// <summary>
    /// Maximum buffer size for JSON parsing.
    /// </summary>
    public int? MaxBufferSize { get; set; }

    /// <summary>
    /// Callback for stderr output.
    /// </summary>
    public Action<string>? StderrCallback { get; set; }

    /// <summary>
    /// Hook configurations by event type.
    /// </summary>
    public Dictionary<HookEvent, List<HookMatcher>>? Hooks { get; set; }

    /// <summary>
    /// Beta features to enable.
    /// </summary>
    public List<string> Betas { get; set; } = [];

    /// <summary>
    /// Agent definitions for multi-agent scenarios.
    /// </summary>
    public Dictionary<string, AgentDefinition>? Agents { get; set; }

    /// <summary>
    /// Plugin configurations.
    /// </summary>
    public List<PluginConfig> Plugins { get; set; } = [];

    /// <summary>
    /// Sandbox settings.
    /// </summary>
    public SandboxSettings? Sandbox { get; set; }

    /// <summary>
    /// Maximum tokens for thinking blocks.
    /// </summary>
    public int? MaxThinkingTokens { get; set; }

    /// <summary>
    /// Output format configuration (JSON schema).
    /// </summary>
    public Dictionary<string, object?>? OutputFormat { get; set; }

    /// <summary>
    /// Whether to enable file checkpointing for rewind support.
    /// </summary>
    public bool EnableFileCheckpointing { get; set; }
}

/// <summary>
/// Base class for MCP server configurations.
/// </summary>
public abstract class McpServerConfig
{
    /// <summary>
    /// The type of MCP server transport.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Configuration for stdio-based MCP server.
/// </summary>
public sealed class McpStdioServerConfig : McpServerConfig
{
    /// <inheritdoc />
    public override string Type => "stdio";

    /// <summary>
    /// Command to execute.
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; set; }

    /// <summary>
    /// Command arguments.
    /// </summary>
    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }

    /// <summary>
    /// Environment variables for the command.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }
}

/// <summary>
/// Configuration for SSE-based MCP server.
/// </summary>
public sealed class McpSseServerConfig : McpServerConfig
{
    /// <inheritdoc />
    public override string Type => "sse";

    /// <summary>
    /// SSE endpoint URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// HTTP headers to include.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Configuration for HTTP-based MCP server.
/// </summary>
public sealed class McpHttpServerConfig : McpServerConfig
{
    /// <inheritdoc />
    public override string Type => "http";

    /// <summary>
    /// HTTP endpoint URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// HTTP headers to include.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Definition for a sub-agent.
/// </summary>
public sealed class AgentDefinition
{
    /// <summary>
    /// Name of the agent.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Description of what the agent does.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// System prompt for the agent.
    /// </summary>
    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Tools available to the agent.
    /// </summary>
    [JsonPropertyName("tools")]
    public List<string>? Tools { get; set; }

    /// <summary>
    /// Model to use for the agent.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>
/// Plugin configuration.
/// </summary>
public sealed class PluginConfig
{
    /// <summary>
    /// Path to the plugin directory.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <summary>
    /// Plugin-specific configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public Dictionary<string, object?>? Config { get; set; }
}

/// <summary>
/// Sandbox configuration settings.
/// </summary>
public sealed class SandboxSettings
{
    /// <summary>
    /// Whether to enable sandboxing.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Sandbox type (e.g., "docker", "none").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Additional sandbox configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public Dictionary<string, object?>? Config { get; set; }
}
