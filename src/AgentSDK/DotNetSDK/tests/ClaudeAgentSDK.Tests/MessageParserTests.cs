using System.Text.Json;
using ClaudeAgentSDK.Internal;
using ClaudeAgentSDK.Models;
using FluentAssertions;
using Xunit;

namespace ClaudeAgentSDK.Tests;

public class MessageParserTests
{
    [Fact]
    public void ParseMessage_UserMessage_WithStringContent()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "user",
            ["uuid"] = "test-uuid",
            ["message"] = JsonSerializer.Deserialize<JsonElement>("""{"content": "Hello world"}""")
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<UserMessage>();
        var userMessage = (UserMessage)result;
        userMessage.Uuid.Should().Be("test-uuid");
        userMessage.GetContentAsString().Should().Be("Hello world");
    }

    [Fact]
    public void ParseMessage_AssistantMessage_WithTextBlock()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "content": [
                        {"type": "text", "text": "Hello!"}
                    ],
                    "model": "claude-sonnet-4-20250514"
                }
            """)
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<AssistantMessage>();
        var assistant = (AssistantMessage)result;
        assistant.Model.Should().Be("claude-sonnet-4-20250514");
        assistant.Content.Should().HaveCount(1);
        assistant.Content[0].Should().BeOfType<TextBlock>();
        ((TextBlock)assistant.Content[0]).Text.Should().Be("Hello!");
    }

    [Fact]
    public void ParseMessage_AssistantMessage_WithToolUseBlock()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "content": [
                        {
                            "type": "tool_use",
                            "id": "tool-123",
                            "name": "read_file",
                            "input": {"path": "/tmp/test.txt"}
                        }
                    ],
                    "model": "claude-sonnet-4-20250514"
                }
            """)
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<AssistantMessage>();
        var assistant = (AssistantMessage)result;
        assistant.Content.Should().HaveCount(1);
        assistant.Content[0].Should().BeOfType<ToolUseBlock>();
        var toolUse = (ToolUseBlock)assistant.Content[0];
        toolUse.Id.Should().Be("tool-123");
        toolUse.Name.Should().Be("read_file");
        toolUse.Input.Should().ContainKey("path");
    }

    [Fact]
    public void ParseMessage_AssistantMessage_WithThinkingBlock()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "content": [
                        {
                            "type": "thinking",
                            "thinking": "Let me think about this...",
                            "signature": "sig123"
                        },
                        {"type": "text", "text": "Here's my answer."}
                    ],
                    "model": "claude-sonnet-4-20250514"
                }
            """)
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<AssistantMessage>();
        var assistant = (AssistantMessage)result;
        assistant.Content.Should().HaveCount(2);
        assistant.Content[0].Should().BeOfType<ThinkingBlock>();
        var thinking = (ThinkingBlock)assistant.Content[0];
        thinking.Thinking.Should().Be("Let me think about this...");
        thinking.Signature.Should().Be("sig123");
    }

    [Fact]
    public void ParseMessage_SystemMessage()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "system",
            ["subtype"] = "init",
            ["extra"] = "data"
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<SystemMessage>();
        var system = (SystemMessage)result;
        system.Subtype.Should().Be("init");
        system.Data.Should().ContainKey("extra");
    }

    [Fact]
    public void ParseMessage_ResultMessage()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 1000,
            ["duration_api_ms"] = 800,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "session-123",
            ["total_cost_usd"] = 0.01,
            ["result"] = "Task completed"
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<ResultMessage>();
        var resultMsg = (ResultMessage)result;
        resultMsg.Subtype.Should().Be("success");
        resultMsg.DurationMs.Should().Be(1000);
        resultMsg.DurationApiMs.Should().Be(800);
        resultMsg.IsError.Should().BeFalse();
        resultMsg.NumTurns.Should().Be(1);
        resultMsg.SessionId.Should().Be("session-123");
        resultMsg.TotalCostUsd.Should().Be(0.01);
        resultMsg.Result.Should().Be("Task completed");
    }

    [Fact]
    public void ParseMessage_StreamEvent()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "stream_event",
            ["uuid"] = "event-123",
            ["session_id"] = "session-456",
            ["event"] = new Dictionary<string, object?>
            {
                ["type"] = "content_block_delta",
                ["delta"] = new Dictionary<string, object?> { ["text"] = "partial" }
            }
        };

        var result = MessageParser.ParseMessage(data);

        result.Should().BeOfType<StreamEvent>();
        var streamEvent = (StreamEvent)result;
        streamEvent.Uuid.Should().Be("event-123");
        streamEvent.SessionId.Should().Be("session-456");
        streamEvent.Event.Should().ContainKey("type");
    }

    [Fact]
    public void ParseMessage_MissingType_ThrowsMessageParseException()
    {
        var data = new Dictionary<string, object?>
        {
            ["content"] = "Hello"
        };

        var action = () => MessageParser.ParseMessage(data);

        action.Should().Throw<MessageParseException>()
            .WithMessage("*'type'*");
    }

    [Fact]
    public void ParseMessage_UnknownType_ThrowsMessageParseException()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "unknown_type"
        };

        var action = () => MessageParser.ParseMessage(data);

        action.Should().Throw<MessageParseException>()
            .WithMessage("*Unknown message type*");
    }

    [Fact]
    public void IsControlRequest_ValidControlRequest_ReturnsTrue()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "req-123"
        };

        var result = MessageParser.IsControlRequest(data);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsControlRequest_RegularMessage_ReturnsFalse()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "assistant"
        };

        var result = MessageParser.IsControlRequest(data);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsControlResponse_ValidControlResponse_ReturnsTrue()
    {
        var data = new Dictionary<string, object?>
        {
            ["type"] = "control_response",
            ["response"] = new Dictionary<string, object?>()
        };

        var result = MessageParser.IsControlResponse(data);

        result.Should().BeTrue();
    }
}
