using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Base class for SDK control requests.
/// </summary>
public abstract class ControlRequest
{
    /// <summary>
    /// The subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public abstract string Subtype { get; }
}

/// <summary>
/// Control request to initialize the SDK control protocol.
/// </summary>
public sealed class InitializeRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "initialize";

    /// <summary>
    /// Protocol version.
    /// </summary>
    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; init; } = 1;
}

/// <summary>
/// Control request to ask for tool permission.
/// </summary>
public sealed class CanUseToolRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "can_use_tool";

    /// <summary>
    /// Name of the tool.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Tool input parameters.
    /// </summary>
    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }

    /// <summary>
    /// Permission context.
    /// </summary>
    [JsonPropertyName("context")]
    public required ToolPermissionContext Context { get; init; }
}

/// <summary>
/// Control request for hook callbacks.
/// </summary>
public sealed class HookCallbackRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "hook_callback";

    /// <summary>
    /// The hook event type.
    /// </summary>
    [JsonPropertyName("hook_event")]
    public required string HookEvent { get; init; }

    /// <summary>
    /// The hook input data.
    /// </summary>
    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }

    /// <summary>
    /// Matcher that triggered this hook.
    /// </summary>
    [JsonPropertyName("matcher")]
    public string? Matcher { get; init; }

    /// <summary>
    /// Hook context.
    /// </summary>
    [JsonPropertyName("context")]
    public required HookContext Context { get; init; }
}

/// <summary>
/// Control request for MCP server messages.
/// </summary>
public sealed class McpMessageRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "mcp_message";

    /// <summary>
    /// Name of the MCP server.
    /// </summary>
    [JsonPropertyName("server_name")]
    public required string ServerName { get; init; }

    /// <summary>
    /// The MCP message content.
    /// </summary>
    [JsonPropertyName("message")]
    public required Dictionary<string, object?> Message { get; init; }
}

/// <summary>
/// Control request to set the permission mode.
/// </summary>
public sealed class SetPermissionModeRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "set_permission_mode";

    /// <summary>
    /// The permission mode to set.
    /// </summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }
}

/// <summary>
/// Control request to set the model.
/// </summary>
public sealed class SetModelRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "set_model";

    /// <summary>
    /// The model name to set (null to use default).
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

/// <summary>
/// Control request to rewind files to a checkpoint.
/// </summary>
public sealed class RewindFilesRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "rewind_files";

    /// <summary>
    /// The user message ID to rewind to.
    /// </summary>
    [JsonPropertyName("user_message_id")]
    public required string UserMessageId { get; init; }
}

/// <summary>
/// Control request to interrupt the current operation.
/// </summary>
public sealed class InterruptRequest : ControlRequest
{
    /// <inheritdoc />
    public override string Subtype => "interrupt";
}

/// <summary>
/// A control request wrapper with request ID.
/// </summary>
public sealed class SdkControlRequest
{
    /// <summary>
    /// Message type identifier.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "control_request";

    /// <summary>
    /// Unique request identifier.
    /// </summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>
    /// The actual request.
    /// </summary>
    [JsonPropertyName("request")]
    public required ControlRequest Request { get; init; }
}

/// <summary>
/// Base class for control responses.
/// </summary>
public abstract class ControlResponse
{
    /// <summary>
    /// Response subtype (success or error).
    /// </summary>
    [JsonPropertyName("subtype")]
    public abstract string Subtype { get; }

    /// <summary>
    /// The request ID this response corresponds to.
    /// </summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }
}

/// <summary>
/// Successful control response.
/// </summary>
public sealed class ControlSuccessResponse : ControlResponse
{
    /// <inheritdoc />
    public override string Subtype => "success";

    /// <summary>
    /// The response data.
    /// </summary>
    [JsonPropertyName("response")]
    public Dictionary<string, object?>? Response { get; init; }
}

/// <summary>
/// Error control response.
/// </summary>
public sealed class ControlErrorResponse : ControlResponse
{
    /// <inheritdoc />
    public override string Subtype => "error";

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

/// <summary>
/// A control response wrapper.
/// </summary>
public sealed class SdkControlResponse
{
    /// <summary>
    /// Message type identifier.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "control_response";

    /// <summary>
    /// The response.
    /// </summary>
    [JsonPropertyName("response")]
    public required ControlResponse Response { get; init; }
}
