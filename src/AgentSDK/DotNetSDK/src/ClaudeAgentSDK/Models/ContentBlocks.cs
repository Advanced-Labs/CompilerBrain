using System.Text.Json.Serialization;

namespace ClaudeAgentSDK.Models;

/// <summary>
/// Base interface for content blocks in messages.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
public interface IContentBlock
{
    /// <summary>
    /// The type of content block.
    /// </summary>
    string Type { get; }
}

/// <summary>
/// A text content block.
/// </summary>
public sealed record TextBlock : IContentBlock
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "text";

    /// <summary>
    /// The text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// A thinking content block containing extended thinking output.
/// </summary>
public sealed record ThinkingBlock : IContentBlock
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "thinking";

    /// <summary>
    /// The thinking content.
    /// </summary>
    [JsonPropertyName("thinking")]
    public required string Thinking { get; init; }

    /// <summary>
    /// The cryptographic signature for the thinking block.
    /// </summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

/// <summary>
/// A tool use content block indicating a tool invocation.
/// </summary>
public sealed record ToolUseBlock : IContentBlock
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "tool_use";

    /// <summary>
    /// Unique identifier for this tool use.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The name of the tool being invoked.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The input parameters for the tool.
    /// </summary>
    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }
}

/// <summary>
/// A tool result content block containing the output from a tool execution.
/// </summary>
public sealed record ToolResultBlock : IContentBlock
{
    /// <inheritdoc />
    [JsonPropertyName("type")]
    public string Type => "tool_result";

    /// <summary>
    /// The ID of the tool use this result corresponds to.
    /// </summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>
    /// The content of the tool result (can be a string or list of content blocks).
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; init; }

    /// <summary>
    /// Whether the tool execution resulted in an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }
}
