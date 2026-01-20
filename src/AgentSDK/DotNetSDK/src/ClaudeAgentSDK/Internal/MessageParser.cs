using System.Text.Json;
using ClaudeAgentSDK.Models;

namespace ClaudeAgentSDK.Internal;

/// <summary>
/// Parses raw JSON messages from CLI into typed message objects.
/// </summary>
public static class MessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Parses a raw JSON dictionary into a typed message.
    /// </summary>
    /// <param name="data">The raw JSON data as a dictionary.</param>
    /// <returns>A typed message object.</returns>
    /// <exception cref="MessageParseException">Thrown when the message cannot be parsed.</exception>
    public static IMessage ParseMessage(Dictionary<string, object?> data)
    {
        // Handle both string and JsonElement (from JSON deserialization)
        var type = GetStringProperty(data, "type")
            ?? throw new MessageParseException("Message missing 'type' field", data);

        return type switch
        {
            "user" => ParseUserMessage(data),
            "assistant" => ParseAssistantMessage(data),
            "system" => ParseSystemMessage(data),
            "result" => ParseResultMessage(data),
            "stream_event" => ParseStreamEvent(data),
            _ => throw new MessageParseException($"Unknown message type: {type}", data)
        };
    }

    /// <summary>
    /// Checks if a message is a control request.
    /// </summary>
    public static bool IsControlRequest(Dictionary<string, object?> data)
    {
        return GetStringProperty(data, "type") == "control_request";
    }

    /// <summary>
    /// Checks if a message is a control response.
    /// </summary>
    public static bool IsControlResponse(Dictionary<string, object?> data)
    {
        return GetStringProperty(data, "type") == "control_response";
    }

    private static UserMessage ParseUserMessage(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("message", out var messageObj) || messageObj is not JsonElement messageElement)
        {
            throw new MessageParseException("User message missing 'message' field", data);
        }

        var content = ParseMessageContent(messageElement);
        var uuid = GetStringProperty(data, "uuid");
        var parentToolUseId = GetNestedStringProperty(messageElement, "parent_tool_use_id");

        return new UserMessage
        {
            Content = content,
            Uuid = uuid,
            ParentToolUseId = parentToolUseId
        };
    }

    private static AssistantMessage ParseAssistantMessage(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("message", out var messageObj) || messageObj is not JsonElement messageElement)
        {
            throw new MessageParseException("Assistant message missing 'message' field", data);
        }

        var contentBlocks = ParseContentBlocks(messageElement);
        var model = GetNestedStringProperty(messageElement, "model")
            ?? throw new MessageParseException("Assistant message missing 'model' field", data);

        var parentToolUseId = GetNestedStringProperty(messageElement, "parent_tool_use_id");
        var error = ParseAssistantError(messageElement);

        return new AssistantMessage
        {
            Content = contentBlocks,
            Model = model,
            ParentToolUseId = parentToolUseId,
            Error = error
        };
    }

    private static SystemMessage ParseSystemMessage(Dictionary<string, object?> data)
    {
        var subtype = GetStringProperty(data, "subtype")
            ?? throw new MessageParseException("System message missing 'subtype' field", data);

        return new SystemMessage
        {
            Subtype = subtype,
            Data = data
        };
    }

    private static ResultMessage ParseResultMessage(Dictionary<string, object?> data)
    {
        var subtype = GetStringProperty(data, "subtype")
            ?? throw new MessageParseException("Result message missing 'subtype' field", data);
        var sessionId = GetStringProperty(data, "session_id")
            ?? throw new MessageParseException("Result message missing 'session_id' field", data);

        return new ResultMessage
        {
            Subtype = subtype,
            DurationMs = GetIntProperty(data, "duration_ms") ?? 0,
            DurationApiMs = GetIntProperty(data, "duration_api_ms") ?? 0,
            IsError = GetBoolProperty(data, "is_error") ?? false,
            NumTurns = GetIntProperty(data, "num_turns") ?? 0,
            SessionId = sessionId,
            TotalCostUsd = GetDoubleProperty(data, "total_cost_usd"),
            Usage = GetDictProperty(data, "usage"),
            Result = GetStringProperty(data, "result"),
            StructuredOutput = data.GetValueOrDefault("structured_output")
        };
    }

    private static StreamEvent ParseStreamEvent(Dictionary<string, object?> data)
    {
        var uuid = GetStringProperty(data, "uuid")
            ?? throw new MessageParseException("Stream event missing 'uuid' field", data);
        var sessionId = GetStringProperty(data, "session_id")
            ?? throw new MessageParseException("Stream event missing 'session_id' field", data);

        var eventData = GetDictProperty(data, "event")
            ?? throw new MessageParseException("Stream event missing 'event' field", data);

        return new StreamEvent
        {
            Uuid = uuid,
            SessionId = sessionId,
            Event = eventData,
            ParentToolUseId = GetStringProperty(data, "parent_tool_use_id")
        };
    }

    private static object ParseMessageContent(JsonElement messageElement)
    {
        if (messageElement.TryGetProperty("content", out var contentElement))
        {
            if (contentElement.ValueKind == JsonValueKind.String)
            {
                return contentElement.GetString()!;
            }

            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                return ParseContentBlocksFromElement(contentElement);
            }
        }

        return "";
    }

    private static List<IContentBlock> ParseContentBlocks(JsonElement messageElement)
    {
        if (messageElement.TryGetProperty("content", out var contentElement) &&
            contentElement.ValueKind == JsonValueKind.Array)
        {
            return ParseContentBlocksFromElement(contentElement);
        }

        return [];
    }

    private static List<IContentBlock> ParseContentBlocksFromElement(JsonElement contentElement)
    {
        var blocks = new List<IContentBlock>();

        foreach (var blockElement in contentElement.EnumerateArray())
        {
            var block = ParseContentBlock(blockElement);
            if (block != null)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private static IContentBlock? ParseContentBlock(JsonElement blockElement)
    {
        if (!blockElement.TryGetProperty("type", out var typeElement))
        {
            return null;
        }

        var type = typeElement.GetString();

        return type switch
        {
            "text" => new TextBlock
            {
                Text = blockElement.GetProperty("text").GetString() ?? ""
            },
            "thinking" => new ThinkingBlock
            {
                Thinking = blockElement.GetProperty("thinking").GetString() ?? "",
                Signature = blockElement.GetProperty("signature").GetString() ?? ""
            },
            "tool_use" => new ToolUseBlock
            {
                Id = blockElement.GetProperty("id").GetString() ?? "",
                Name = blockElement.GetProperty("name").GetString() ?? "",
                Input = JsonElementToDict(blockElement.GetProperty("input"))
            },
            "tool_result" => new ToolResultBlock
            {
                ToolUseId = blockElement.GetProperty("tool_use_id").GetString() ?? "",
                Content = blockElement.TryGetProperty("content", out var content) ? ParseToolResultContent(content) : null,
                IsError = blockElement.TryGetProperty("is_error", out var isError) && isError.GetBoolean()
            },
            _ => null
        };
    }

    private static object? ParseToolResultContent(JsonElement contentElement)
    {
        return contentElement.ValueKind switch
        {
            JsonValueKind.String => contentElement.GetString(),
            JsonValueKind.Array => contentElement.Deserialize<List<Dictionary<string, object?>>>(),
            _ => null
        };
    }

    private static AssistantMessageError? ParseAssistantError(JsonElement messageElement)
    {
        if (messageElement.TryGetProperty("error", out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.Object)
        {
            return new AssistantMessageError
            {
                Code = errorElement.TryGetProperty("code", out var code) ? code.GetString() : null,
                Message = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : null
            };
        }

        return null;
    }

    private static string? GetStringProperty(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is string s) return s;
            if (value is JsonElement { ValueKind: JsonValueKind.String } element) return element.GetString();
        }
        return null;
    }

    private static string? GetNestedStringProperty(JsonElement element, string key)
    {
        if (element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static int? GetIntProperty(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is JsonElement { ValueKind: JsonValueKind.Number } element) return element.GetInt32();
        }
        return null;
    }

    private static double? GetDoubleProperty(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is JsonElement { ValueKind: JsonValueKind.Number } element) return element.GetDouble();
        }
        return null;
    }

    private static bool? GetBoolProperty(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is bool b) return b;
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
            }
        }
        return null;
    }

    private static Dictionary<string, object?>? GetDictProperty(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is Dictionary<string, object?> dict) return dict;
            if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
            {
                return JsonElementToDict(element);
            }
        }
        return null;
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => JsonElementToDict(element),
            _ => null
        };
    }
}
