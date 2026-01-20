using ClaudeAgentSDK.Models;
using FluentAssertions;
using Xunit;

namespace ClaudeAgentSDK.Tests;

public class ModelsTests
{
    [Fact]
    public void TextBlock_HasCorrectType()
    {
        var block = new TextBlock { Text = "Hello" };
        block.Type.Should().Be("text");
        block.Text.Should().Be("Hello");
    }

    [Fact]
    public void ThinkingBlock_HasCorrectType()
    {
        var block = new ThinkingBlock
        {
            Thinking = "Thinking...",
            Signature = "sig"
        };
        block.Type.Should().Be("thinking");
    }

    [Fact]
    public void ToolUseBlock_HasCorrectType()
    {
        var block = new ToolUseBlock
        {
            Id = "tool-1",
            Name = "read_file",
            Input = new Dictionary<string, object?> { ["path"] = "/tmp" }
        };
        block.Type.Should().Be("tool_use");
        block.Input.Should().ContainKey("path");
    }

    [Fact]
    public void ToolResultBlock_HasCorrectType()
    {
        var block = new ToolResultBlock
        {
            ToolUseId = "tool-1",
            Content = "file contents",
            IsError = false
        };
        block.Type.Should().Be("tool_result");
    }

    [Fact]
    public void UserMessage_GetContentAsString_ReturnsString()
    {
        var message = new UserMessage
        {
            Content = "Hello world",
            Uuid = "123"
        };

        message.GetContentAsString().Should().Be("Hello world");
        message.GetContentBlocks().Should().BeNull();
    }

    [Fact]
    public void AssistantMessage_HasContentBlocks()
    {
        var blocks = new List<IContentBlock>
        {
            new TextBlock { Text = "Hello" }
        };

        var message = new AssistantMessage
        {
            Content = blocks,
            Model = "claude-sonnet-4-20250514"
        };

        message.Content.Should().HaveCount(1);
        message.Model.Should().Be("claude-sonnet-4-20250514");
    }

    [Fact]
    public void ResultMessage_PropertiesAreSet()
    {
        var message = new ResultMessage
        {
            Subtype = "success",
            DurationMs = 1000,
            DurationApiMs = 800,
            IsError = false,
            NumTurns = 3,
            SessionId = "session-1",
            TotalCostUsd = 0.05,
            Result = "Done"
        };

        message.Type.Should().Be("result");
        message.DurationMs.Should().Be(1000);
        message.TotalCostUsd.Should().Be(0.05);
    }

    [Fact]
    public void ClaudeAgentOptions_DefaultValues()
    {
        var options = new ClaudeAgentOptions();

        options.AllowedTools.Should().BeEmpty();
        options.DisallowedTools.Should().BeEmpty();
        options.AddDirs.Should().BeEmpty();
        options.Environment.Should().BeEmpty();
        options.Betas.Should().BeEmpty();
        options.Plugins.Should().BeEmpty();
        options.ContinueConversation.Should().BeFalse();
        options.IncludePartialMessages.Should().BeFalse();
    }

    [Fact]
    public void PermissionResultAllow_HasCorrectBehavior()
    {
        var result = new PermissionResultAllow
        {
            UpdatedInput = new Dictionary<string, object?> { ["key"] = "value" }
        };

        result.Behavior.Should().Be("allow");
        result.UpdatedInput.Should().ContainKey("key");
    }

    [Fact]
    public void PermissionResultDeny_HasCorrectBehavior()
    {
        var result = new PermissionResultDeny
        {
            Message = "Not allowed",
            Interrupt = true
        };

        result.Behavior.Should().Be("deny");
        result.Message.Should().Be("Not allowed");
        result.Interrupt.Should().BeTrue();
    }

    [Fact]
    public void HookMatcher_CanBeConfigured()
    {
        var matcher = new HookMatcher
        {
            Matcher = "Bash",
            Timeout = 30.0,
            Hooks =
            [
                new HookCallback
                {
                    Handler = (_, _, _) => Task.FromResult(new HookOutput { Continue = true })
                }
            ]
        };

        matcher.Matcher.Should().Be("Bash");
        matcher.Timeout.Should().Be(30.0);
        matcher.Hooks.Should().HaveCount(1);
    }

    [Fact]
    public void McpStdioServerConfig_HasCorrectType()
    {
        var config = new McpStdioServerConfig
        {
            Command = "node",
            Args = ["server.js"],
            Env = new Dictionary<string, string> { ["NODE_ENV"] = "production" }
        };

        config.Type.Should().Be("stdio");
        config.Command.Should().Be("node");
        config.Args.Should().Contain("server.js");
    }

    [Fact]
    public void McpSseServerConfig_HasCorrectType()
    {
        var config = new McpSseServerConfig
        {
            Url = "https://example.com/sse",
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" }
        };

        config.Type.Should().Be("sse");
        config.Url.Should().Be("https://example.com/sse");
    }

    [Fact]
    public void McpHttpServerConfig_HasCorrectType()
    {
        var config = new McpHttpServerConfig
        {
            Url = "https://example.com/api"
        };

        config.Type.Should().Be("http");
    }
}
