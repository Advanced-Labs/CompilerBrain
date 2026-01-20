using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Context information for a tool permission request.
/// </summary>
public sealed class ToolPermissionContext
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

    /// <summary>
    /// Absolute paths of files that have been read.
    /// </summary>
    [JsonPropertyName("readable_paths")]
    public List<string>? ReadablePaths { get; init; }

    /// <summary>
    /// Absolute paths of files that have been written.
    /// </summary>
    [JsonPropertyName("writable_paths")]
    public List<string>? WritablePaths { get; init; }
}

/// <summary>
/// A request for permission to use a tool.
/// </summary>
public sealed class ToolPermissionRequest
{
    /// <summary>
    /// The name of the tool being invoked.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// The input parameters for the tool.
    /// </summary>
    public required Dictionary<string, object?> Input { get; init; }

    /// <summary>
    /// Context information for the permission request.
    /// </summary>
    public required ToolPermissionContext Context { get; init; }
}

/// <summary>
/// Base class for permission results.
/// </summary>
public abstract class PermissionResult
{
    /// <summary>
    /// The permission behavior.
    /// </summary>
    [JsonPropertyName("behavior")]
    public abstract string Behavior { get; }
}

/// <summary>
/// Permission result that allows the tool execution.
/// </summary>
public sealed class PermissionResultAllow : PermissionResult
{
    /// <inheritdoc />
    public override string Behavior => "allow";

    /// <summary>
    /// Updated input parameters, if any.
    /// </summary>
    [JsonPropertyName("updatedInput")]
    public Dictionary<string, object?>? UpdatedInput { get; init; }

    /// <summary>
    /// Updates to apply to the permission rules.
    /// </summary>
    [JsonPropertyName("updatedPermissions")]
    public List<PermissionUpdate>? UpdatedPermissions { get; init; }
}

/// <summary>
/// Permission result that denies the tool execution.
/// </summary>
public sealed class PermissionResultDeny : PermissionResult
{
    /// <inheritdoc />
    public override string Behavior => "deny";

    /// <summary>
    /// Message explaining why the permission was denied.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    /// <summary>
    /// Whether to interrupt the current conversation.
    /// </summary>
    [JsonPropertyName("interrupt")]
    public bool Interrupt { get; init; }
}

/// <summary>
/// Type of permission update.
/// </summary>
public enum PermissionUpdateType
{
    /// <summary>
    /// Add new rules to existing rules.
    /// </summary>
    AddRules,

    /// <summary>
    /// Replace existing rules.
    /// </summary>
    ReplaceRules,

    /// <summary>
    /// Remove specified rules.
    /// </summary>
    RemoveRules,

    /// <summary>
    /// Set the permission mode.
    /// </summary>
    SetMode,

    /// <summary>
    /// Add directories to the allowed list.
    /// </summary>
    AddDirectories,

    /// <summary>
    /// Remove directories from the allowed list.
    /// </summary>
    RemoveDirectories
}

/// <summary>
/// Permission behavior type.
/// </summary>
public enum PermissionBehavior
{
    /// <summary>
    /// Allow the operation.
    /// </summary>
    Allow,

    /// <summary>
    /// Deny the operation.
    /// </summary>
    Deny,

    /// <summary>
    /// Ask for confirmation.
    /// </summary>
    Ask
}

/// <summary>
/// Destination for permission updates.
/// </summary>
public enum PermissionUpdateDestination
{
    /// <summary>
    /// Apply to session only.
    /// </summary>
    Session,

    /// <summary>
    /// Apply to project settings.
    /// </summary>
    Project,

    /// <summary>
    /// Apply to global user settings.
    /// </summary>
    User
}

/// <summary>
/// A permission rule value.
/// </summary>
public sealed class PermissionRuleValue
{
    /// <summary>
    /// The tool name pattern (supports wildcards).
    /// </summary>
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    /// <summary>
    /// The path pattern (optional).
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>
    /// The behavior to apply.
    /// </summary>
    [JsonPropertyName("behavior")]
    public required PermissionBehavior Behavior { get; init; }
}

/// <summary>
/// An update to permission rules.
/// </summary>
public sealed class PermissionUpdate
{
    /// <summary>
    /// The type of update.
    /// </summary>
    [JsonPropertyName("type")]
    public required PermissionUpdateType Type { get; init; }

    /// <summary>
    /// Rules to add, replace, or remove.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<PermissionRuleValue>? Rules { get; init; }

    /// <summary>
    /// The behavior for the update (for rule updates).
    /// </summary>
    [JsonPropertyName("behavior")]
    public PermissionBehavior? Behavior { get; init; }

    /// <summary>
    /// The mode to set (for SetMode updates).
    /// </summary>
    [JsonPropertyName("mode")]
    public PermissionMode? Mode { get; init; }

    /// <summary>
    /// Directories to add or remove.
    /// </summary>
    [JsonPropertyName("directories")]
    public List<string>? Directories { get; init; }

    /// <summary>
    /// Destination for the update.
    /// </summary>
    [JsonPropertyName("destination")]
    public PermissionUpdateDestination? Destination { get; init; }
}
