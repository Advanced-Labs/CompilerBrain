using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Hook event types.
/// </summary>
public enum HookEvent
{
    /// <summary>
    /// Before a tool is used.
    /// </summary>
    PreToolUse,

    /// <summary>
    /// After a tool is used.
    /// </summary>
    PostToolUse,

    /// <summary>
    /// When a user prompt is submitted.
    /// </summary>
    UserPromptSubmit,

    /// <summary>
    /// When the agent stops.
    /// </summary>
    Stop,

    /// <summary>
    /// When a subagent stops.
    /// </summary>
    SubagentStop,

    /// <summary>
    /// Before context compaction.
    /// </summary>
    PreCompact
}

/// <summary>
/// A hook matcher that determines when hooks should be invoked.
/// </summary>
public sealed class HookMatcher
{
    /// <summary>
    /// Pattern to match (e.g., tool name for tool hooks).
    /// </summary>
    [JsonPropertyName("matcher")]
    public string? Matcher { get; init; }

    /// <summary>
    /// List of hook callbacks to invoke when matched.
    /// </summary>
    [JsonPropertyName("hooks")]
    public List<HookCallback> Hooks { get; init; } = [];

    /// <summary>
    /// Timeout for hook execution in seconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    public double? Timeout { get; init; }
}

/// <summary>
/// A hook callback definition.
/// </summary>
public sealed class HookCallback
{
    /// <summary>
    /// The callback function to invoke.
    /// </summary>
    public required Func<HookInput, string?, HookContext, Task<HookOutput>> Handler { get; init; }
}

/// <summary>
/// Context information for hook execution.
/// </summary>
public sealed class HookContext
{
    /// <summary>
    /// The current working directory.
    /// </summary>
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }

    /// <summary>
    /// The session ID.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}

/// <summary>
/// Base class for hook input.
/// </summary>
public abstract class HookInput
{
    /// <summary>
    /// The type of hook event.
    /// </summary>
    [JsonPropertyName("hook_event")]
    public abstract HookEvent HookEvent { get; }
}

/// <summary>
/// Input for pre-tool-use hooks.
/// </summary>
public sealed class PreToolUseHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.PreToolUse;

    /// <summary>
    /// Name of the tool being used.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Input parameters for the tool.
    /// </summary>
    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }
}

/// <summary>
/// Input for post-tool-use hooks.
/// </summary>
public sealed class PostToolUseHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.PostToolUse;

    /// <summary>
    /// Name of the tool that was used.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Input parameters that were used.
    /// </summary>
    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }

    /// <summary>
    /// Output from the tool execution.
    /// </summary>
    [JsonPropertyName("tool_output")]
    public object? ToolOutput { get; init; }

    /// <summary>
    /// Whether the tool execution resulted in an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    public bool IsError { get; init; }
}

/// <summary>
/// Input for user prompt submit hooks.
/// </summary>
public sealed class UserPromptSubmitHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.UserPromptSubmit;

    /// <summary>
    /// The user's prompt.
    /// </summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
}

/// <summary>
/// Input for stop hooks.
/// </summary>
public sealed class StopHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.Stop;

    /// <summary>
    /// The reason for stopping.
    /// </summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }
}

/// <summary>
/// Input for subagent stop hooks.
/// </summary>
public sealed class SubagentStopHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.SubagentStop;

    /// <summary>
    /// The name of the subagent that stopped.
    /// </summary>
    [JsonPropertyName("agent_name")]
    public required string AgentName { get; init; }

    /// <summary>
    /// The reason for stopping.
    /// </summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }
}

/// <summary>
/// Input for pre-compact hooks.
/// </summary>
public sealed class PreCompactHookInput : HookInput
{
    /// <inheritdoc />
    public override HookEvent HookEvent => HookEvent.PreCompact;

    /// <summary>
    /// Current context size in tokens.
    /// </summary>
    [JsonPropertyName("context_size")]
    public int ContextSize { get; init; }
}

/// <summary>
/// Output from a hook callback.
/// </summary>
public class HookOutput
{
    /// <summary>
    /// Whether to continue execution.
    /// </summary>
    [JsonPropertyName("continue")]
    public bool Continue { get; init; } = true;

    /// <summary>
    /// Whether to suppress output from this operation.
    /// </summary>
    [JsonPropertyName("suppressOutput")]
    public bool SuppressOutput { get; init; }

    /// <summary>
    /// Stop reason to set.
    /// </summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    /// <summary>
    /// Decision (e.g., "block" to block the operation).
    /// </summary>
    [JsonPropertyName("decision")]
    public string? Decision { get; init; }

    /// <summary>
    /// System message to inject.
    /// </summary>
    [JsonPropertyName("systemMessage")]
    public string? SystemMessage { get; init; }

    /// <summary>
    /// Reason for the decision.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Hook-specific output data.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    public Dictionary<string, object?>? HookSpecificOutput { get; init; }
}

/// <summary>
/// Async hook output indicating the hook should run asynchronously.
/// </summary>
public sealed class AsyncHookOutput : HookOutput
{
    /// <summary>
    /// Whether this is an async hook.
    /// </summary>
    [JsonPropertyName("async")]
    public bool IsAsync => true;

    /// <summary>
    /// Timeout for async hook execution in milliseconds.
    /// </summary>
    [JsonPropertyName("asyncTimeout")]
    public int? AsyncTimeout { get; init; }
}
