using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Base interface for all message types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ResultMessage), "result")]
[JsonDerivedType(typeof(StreamEvent), "stream_event")]
public interface IMessage
{
    /// <summary>
    /// The type of message.
    /// </summary>
    string Type { get; }
}

/// <summary>
/// Error information for assistant messages.
/// </summary>
public sealed record AssistantMessageError
{
    /// <summary>
    /// The error code.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// A message from the user.
/// </summary>
public sealed record UserMessage : IMessage
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "user";

    /// <summary>
    /// The content of the message. Can be a string or a list of content blocks.
    /// </summary>
    [JsonPropertyName("content")]
    public required object Content { get; init; }

    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>
    /// The parent tool use ID if this message is in response to a tool use.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    /// Gets the content as a string if it's a simple string message.
    /// </summary>
    public string? GetContentAsString() => Content as string;

    /// <summary>
    /// Gets the content as a list of content blocks if it's a structured message.
    /// </summary>
    public IReadOnlyList<IContentBlock>? GetContentBlocks() => Content as IReadOnlyList<IContentBlock>;
}

/// <summary>
/// A message from the assistant.
/// </summary>
public sealed record AssistantMessage : IMessage
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "assistant";

    /// <summary>
    /// The content blocks of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required IReadOnlyList<IContentBlock> Content { get; init; }

    /// <summary>
    /// The model that generated this message.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// The parent tool use ID if this message is within a tool execution context.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    /// Error information if the message generation encountered an error.
    /// </summary>
    [JsonPropertyName("error")]
    public AssistantMessageError? Error { get; init; }
}

/// <summary>
/// A system message containing metadata or status information.
/// </summary>
public sealed record SystemMessage : IMessage
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "system";

    /// <summary>
    /// The subtype of system message.
    /// </summary>
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    /// <summary>
    /// Additional data associated with the system message.
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object?>? Data { get; init; }
}

/// <summary>
/// A result message indicating the completion of a conversation turn.
/// </summary>
public sealed record ResultMessage : IMessage
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "result";

    /// <summary>
    /// The subtype of result message.
    /// </summary>
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    /// <summary>
    /// Total duration of the turn in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    /// <summary>
    /// Duration of API calls in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_api_ms")]
    public required int DurationApiMs { get; init; }

    /// <summary>
    /// Whether the result represents an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    public required bool IsError { get; init; }

    /// <summary>
    /// Number of turns in this conversation.
    /// </summary>
    [JsonPropertyName("num_turns")]
    public required int NumTurns { get; init; }

    /// <summary>
    /// The session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Total cost in USD for this conversation.
    /// </summary>
    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; init; }

    /// <summary>
    /// Token usage information.
    /// </summary>
    [JsonPropertyName("usage")]
    public Dictionary<string, object?>? Usage { get; init; }

    /// <summary>
    /// The final result text.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>
    /// Structured output if output schema was specified.
    /// </summary>
    [JsonPropertyName("structured_output")]
    public object? StructuredOutput { get; init; }
}

/// <summary>
/// A streaming event from the Anthropic API.
/// </summary>
public sealed record StreamEvent : IMessage
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "stream_event";

    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    /// <summary>
    /// The session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// The raw Anthropic API stream event data.
    /// </summary>
    [JsonPropertyName("event")]
    public required Dictionary<string, object?> Event { get; init; }

    /// <summary>
    /// The parent tool use ID if this event is within a tool execution context.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}
