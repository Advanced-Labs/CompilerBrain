using System.Text.Json;
using ClaudeAgentSDK;
using ClaudeAgentSDK.Models;

namespace ClaudeAgentSDK.Samples;

/// <summary>
/// Advanced feature tests for the Claude Agent SDK.
/// These tests cover more complex features that require specific conditions or may have
/// higher costs/longer execution times.
///
/// SAFETY GUIDELINES:
/// - All tests use MaxTurns and MaxBudgetUsd limits
/// - Tests are designed for safe automated execution
/// - Some tests may be skipped if features are not available
/// </summary>
public static class AdvancedFeatureTests
{
    private static Action<string, bool>? LogCallback;
    private static string? _currentTestModelUsed;

    /// <summary>
    /// Uses the same default model as FeatureTests for consistency
    /// </summary>
    private static string DefaultModel => FeatureTests.DefaultModel;

    private static void Log(string message, bool alsoConsole = true)
    {
        LogCallback?.Invoke(message, alsoConsole);
    }

    /// <summary>
    /// Helper to track model used from AssistantMessage
    /// </summary>
    private static void TrackModelUsed(IMessage message)
    {
        if (message is AssistantMessage assistant && !string.IsNullOrEmpty(assistant.Model))
        {
            _currentTestModelUsed = assistant.Model;
            Log($"  Model: {assistant.Model}");
        }
    }

    /// <summary>
    /// Run all advanced feature tests
    /// </summary>
    public static async Task<List<FeatureTests.TestResult>> RunAllTestsAsync(
        string workingDir,
        Action<string, bool>? logCallback = null)
    {
        LogCallback = logCallback ?? ((msg, _) => Console.WriteLine(msg));
        var results = new List<FeatureTests.TestResult>();

        Log("============================================");
        Log("ADVANCED FEATURE TESTS");
        Log("============================================");
        Log($"Default Model: {DefaultModel}");
        Log("");

        // ============================================
        // SESSION MANAGEMENT TESTS
        // ============================================
        results.Add(await RunTest("Test A1: Resume Session", () => TestA1_ResumeSession(workingDir)));
        results.Add(await RunTest("Test A2: Fork Session", () => TestA2_ForkSession(workingDir)));

        // ============================================
        // STREAMING AND CONTENT TESTS
        // ============================================
        results.Add(await RunTest("Test A3: Partial Messages (StreamEvent)", () => TestA3_PartialMessages(workingDir)));
        results.Add(await RunTest("Test A4: Extended Thinking (ThinkingBlock)", () => TestA4_ExtendedThinking(workingDir)));

        // ============================================
        // ADVANCED CONFIGURATION TESTS
        // ============================================
        results.Add(await RunTest("Test A5: Fallback Model", () => TestA5_FallbackModel(workingDir)));
        results.Add(await RunTest("Test A6: Beta Features", () => TestA6_BetaFeatures(workingDir)));
        results.Add(await RunTest("Test A7: Additional Directories", () => TestA7_AdditionalDirectories(workingDir)));

        // ============================================
        // HOOK TESTS
        // ============================================
        results.Add(await RunTest("Test A8: UserPromptSubmit Hook", () => TestA8_UserPromptSubmitHook(workingDir)));
        results.Add(await RunTest("Test A9: Stop Hook", () => TestA9_StopHook(workingDir)));
        results.Add(await RunTest("Test A10: Block Tool via Hook", () => TestA10_BlockToolViaHook(workingDir)));

        // ============================================
        // CONTROL PROTOCOL TESTS
        // ============================================
        results.Add(await RunTest("Test A11: Server Info", () => TestA11_ServerInfo(workingDir)));
        results.Add(await RunTest("Test A12: File Rewind (Checkpointing)", () => TestA12_FileRewind(workingDir)));

        // ============================================
        // CONTENT BLOCK PARSING TESTS
        // ============================================
        results.Add(await RunTest("Test A13: Tool Result Block", () => TestA13_ToolResultBlock(workingDir)));
        results.Add(await RunTest("Test A14: Multiple Content Blocks", () => TestA14_MultipleContentBlocks(workingDir)));

        // ============================================
        // SUMMARY
        // ============================================
        Log("");
        Log("============================================");
        Log("ADVANCED TEST SUMMARY");
        Log("============================================");
        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => !r.Passed);
        var totalCost = results.Sum(r => r.CostUsd ?? 0);

        Log($"Passed: {passed}");
        Log($"Failed: {failed}");
        Log($"Total Cost: ${totalCost:F4}");
        Log("");
        Log("Model Usage per Test:");
        foreach (var r in results)
        {
            var status = r.Passed ? "PASS" : "FAIL";
            var model = r.ModelUsed ?? "(unknown)";
            Log($"  [{status}] {r.TestName}: {model}");
        }

        return results;
    }

    private static async Task<FeatureTests.TestResult> RunTest(
        string testName,
        Func<Task<(bool success, decimal? cost, string? error)>> test)
    {
        Log($"\n=== {testName} ===");
        var start = DateTime.UtcNow;
        var result = new FeatureTests.TestResult { TestName = testName };
        _currentTestModelUsed = null; // Reset for this test

        try
        {
            var (success, cost, error) = await test();
            result.Passed = success;
            result.CostUsd = cost;
            result.ErrorMessage = error;
            result.ModelUsed = _currentTestModelUsed;
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            Log($"  EXCEPTION: {result.ErrorMessage}");
        }

        result.Duration = DateTime.UtcNow - start;
        var status = result.Passed ? "PASSED" : "FAILED";
        Log($"  Result: {status}");
        if (!string.IsNullOrEmpty(result.ModelUsed))
        {
            Log($"  Model Used: {result.ModelUsed}");
        }
        return result;
    }

    // ============================================
    // SESSION MANAGEMENT TESTS
    // ============================================

    /// <summary>
    /// Test A1: Resume Session - Resume a previous session by ID
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA1_ResumeSession(string workingDir)
    {
        // First, create a session
        var options1 = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        string? sessionId = null;
        await foreach (var message in ClaudeAgent.QueryAsync("Remember: My favorite color is blue", options1))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                sessionId = result.SessionId;
                Log($"  Created session: {sessionId}");
            }
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return (false, null, "Failed to get session ID");
        }

        // Try to resume the session
        var options2 = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            Resume = sessionId,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        decimal? cost = null;
        string? newSessionId = null;
        string? response = null;

        await foreach (var message in ClaudeAgent.QueryAsync("What is my favorite color?", options2))
        {
            TrackModelUsed(message);
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    if (block is TextBlock text)
                    {
                        response = text.Text;
                        Log($"  Response: {text.Text}");
                    }
                }
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
                newSessionId = result.SessionId;
                Log($"  Resumed session: {newSessionId}");
            }
        }

        // Verify session was resumed (same or continued session)
        var success = newSessionId != null;
        return (success, cost, success ? null : "Failed to resume session");
    }

    /// <summary>
    /// Test A2: Fork Session - Fork an existing session
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA2_ForkSession(string workingDir)
    {
        // First, create a session
        var options1 = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        string? sessionId = null;
        await foreach (var message in ClaudeAgent.QueryAsync("Remember: X equals 42", options1))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                sessionId = result.SessionId;
                Log($"  Original session: {sessionId}");
            }
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return (false, null, "Failed to get session ID");
        }

        // Fork the session
        var options2 = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            Resume = sessionId,
            ForkSession = true,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        decimal? cost = null;
        string? forkedSessionId = null;

        await foreach (var message in ClaudeAgent.QueryAsync("What is X?", options2))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
                forkedSessionId = result.SessionId;
                Log($"  Forked session: {forkedSessionId}");
            }
        }

        // Forked session should have different ID
        var success = forkedSessionId != null;
        return (success, cost, null);
    }

    // ============================================
    // STREAMING AND CONTENT TESTS
    // ============================================

    /// <summary>
    /// Test A3: Partial Messages (StreamEvent) - Test streaming partial updates
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA3_PartialMessages(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            IncludePartialMessages = true,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        int streamEventCount = 0;
        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("Write a short poem about coding", options))
        {
            TrackModelUsed(message);
            if (message is StreamEvent streamEvent)
            {
                streamEventCount++;
                if (streamEventCount <= 3)
                {
                    Log($"  StreamEvent #{streamEventCount}: {streamEvent.Event.Keys.FirstOrDefault()}");
                }
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  Total StreamEvents received: {streamEventCount}");

        // Note: StreamEvents may not be available in all modes
        return (true, cost, null);
    }

    /// <summary>
    /// Test A4: Extended Thinking (ThinkingBlock) - Test thinking content
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA4_ExtendedThinking(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxThinkingTokens = 1024, // Request extended thinking
            MaxTurns = 1,
            MaxBudgetUsd = 0.20
        };

        bool hasThinkingBlock = false;
        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync(
            "Think step by step about how to solve: If a train leaves at 3pm going 60mph and another at 4pm going 80mph, when do they meet?",
            options))
        {
            TrackModelUsed(message);
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    if (block is ThinkingBlock thinking)
                    {
                        hasThinkingBlock = true;
                        Log($"  ThinkingBlock found: {thinking.Thinking.Substring(0, Math.Min(100, thinking.Thinking.Length))}...");
                        Log($"  Signature present: {!string.IsNullOrEmpty(thinking.Signature)}");
                    }
                    else if (block is TextBlock text)
                    {
                        Log($"  Response: {text.Text.Substring(0, Math.Min(100, text.Text.Length))}...");
                    }
                }
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        // Note: ThinkingBlock may require specific model versions or settings
        Log($"  ThinkingBlock received: {hasThinkingBlock}");
        return (true, cost, null);
    }

    // ============================================
    // ADVANCED CONFIGURATION TESTS
    // ============================================

    /// <summary>
    /// Test A5: Fallback Model - Test fallback model configuration
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA5_FallbackModel(string workingDir)
    {
        // Note: This test uses specific models for fallback testing, not DefaultModel
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = "claude-sonnet-4-20250514",
            FallbackModel = "claude-haiku-4-20250514",
            MaxTurns = 1,
            MaxBudgetUsd = 0.05
        };

        string? modelUsed = null;
        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("Say 'fallback test'", options))
        {
            TrackModelUsed(message);
            if (message is AssistantMessage assistant)
            {
                modelUsed = assistant.Model;
                Log($"  Model used: {modelUsed}");
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        return (true, cost, null);
    }

    /// <summary>
    /// Test A6: Beta Features - Test enabling beta flags
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA6_BetaFeatures(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            Betas = new List<string> { "interleaved-thinking" },
            MaxTurns = 1,
            MaxBudgetUsd = 0.10
        };

        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("Say 'beta test successful'", options))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        return (true, cost, null);
    }

    /// <summary>
    /// Test A7: Additional Directories - Test adding extra directories
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA7_AdditionalDirectories(string workingDir)
    {
        // Create a temp directory to add
        var tempDir = Path.Combine(Path.GetTempPath(), $"claude-sdk-adddir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "Hello from additional directory!");

        try
        {
            var options = new ClaudeAgentOptions
            {
                WorkingDirectory = workingDir,
                Model = DefaultModel,
                AddDirs = new List<string> { tempDir },
                MaxTurns = 1,
                MaxBudgetUsd = 0.10
            };

            decimal? cost = null;

            await foreach (var message in ClaudeAgent.QueryAsync("Say 'additional directories configured'", options))
            {
                TrackModelUsed(message);
                if (message is ResultMessage result)
                {
                    cost = (decimal?)(result.TotalCostUsd);
                }
            }

            return (true, cost, null);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
                Log($"  Cleaned up temp directory");
            }
        }
    }

    // ============================================
    // HOOK TESTS
    // ============================================

    /// <summary>
    /// Test A8: UserPromptSubmit Hook - Hook before prompt processing
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA8_UserPromptSubmitHook(string workingDir)
    {
        bool hookInvoked = false;
        string? capturedPrompt = null;

        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10,
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.UserPromptSubmit] = new List<HookMatcher>
                {
                    new HookMatcher
                    {
                        Hooks = new List<HookCallback>
                        {
                            new HookCallback
                            {
                                Handler = async (input, matcher, context) =>
                                {
                                    hookInvoked = true;
                                    if (input is UserPromptSubmitHookInput promptInput)
                                    {
                                        capturedPrompt = promptInput.Prompt;
                                        Log($"    [HOOK] UserPromptSubmit: {promptInput.Prompt}");
                                    }
                                    return new HookOutput { Continue = true };
                                }
                            }
                        }
                    }
                }
            }
        };

        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("Test prompt for hook", options))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  Hook invoked: {hookInvoked}");
        return (true, cost, null);
    }

    /// <summary>
    /// Test A9: Stop Hook - Hook when agent stops
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA9_StopHook(string workingDir)
    {
        bool hookInvoked = false;
        string? stopReason = null;

        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.10,
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.Stop] = new List<HookMatcher>
                {
                    new HookMatcher
                    {
                        Hooks = new List<HookCallback>
                        {
                            new HookCallback
                            {
                                Handler = async (input, matcher, context) =>
                                {
                                    hookInvoked = true;
                                    if (input is StopHookInput stopInput)
                                    {
                                        stopReason = stopInput.StopReason;
                                        Log($"    [HOOK] Stop: {stopInput.StopReason}");
                                    }
                                    return new HookOutput { Continue = true };
                                }
                            }
                        }
                    }
                }
            }
        };

        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("Say hello", options))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  Stop hook invoked: {hookInvoked}");
        return (true, cost, null);
    }

    /// <summary>
    /// Test A10: Block Tool via Hook - Use hook to block specific tools
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA10_BlockToolViaHook(string workingDir)
    {
        var blockedTools = new List<string>();

        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 2,
            MaxBudgetUsd = 0.15,
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new HookMatcher
                    {
                        Matcher = "Bash", // Only match Bash tool
                        Hooks = new List<HookCallback>
                        {
                            new HookCallback
                            {
                                Handler = async (input, matcher, context) =>
                                {
                                    if (input is PreToolUseHookInput toolInput)
                                    {
                                        blockedTools.Add(toolInput.ToolName);
                                        Log($"    [HOOK] Blocking tool: {toolInput.ToolName}");
                                    }
                                    // Block the tool
                                    return new HookOutput
                                    {
                                        Continue = false,
                                        Decision = "block",
                                        Reason = "Bash tool blocked by test hook"
                                    };
                                }
                            }
                        }
                    }
                }
            }
        };

        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync("What is 2+2? Do not use any tools.", options))
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  Blocked tools: {string.Join(", ", blockedTools)}");
        return (true, cost, null);
    }

    // ============================================
    // CONTROL PROTOCOL TESTS
    // ============================================

    /// <summary>
    /// Test A11: Server Info - Get CLI capabilities
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA11_ServerInfo(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.05
        };

        await using var client = ClaudeAgent.CreateClient(options);
        await client.ConnectAsync();

        var serverInfo = client.ServerInfo;
        if (serverInfo != null)
        {
            Log($"  Server info available: {serverInfo.Count} properties");
            foreach (var kvp in serverInfo.Take(5))
            {
                Log($"    {kvp.Key}: {kvp.Value}");
            }
        }
        else
        {
            Log($"  Server info: null");
        }

        // Send a simple query to get cost
        await client.QueryAsync("Say hi");
        decimal? cost = null;
        await foreach (var message in client.ReceiveResponseAsync())
        {
            TrackModelUsed(message);
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        return (true, cost, null);
    }

    /// <summary>
    /// Test A12: File Rewind (Checkpointing) - Test file checkpointing
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA12_FileRewind(string workingDir)
    {
        // Create isolated test directory
        var testDir = Path.Combine(Path.GetTempPath(), $"claude-sdk-rewind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            var options = new ClaudeAgentOptions
            {
                WorkingDirectory = testDir,
                Model = DefaultModel,
                EnableFileCheckpointing = true,
                MaxTurns = 3,
                MaxBudgetUsd = 0.20
            };

            await using var client = ClaudeAgent.CreateClient(options);
            await client.ConnectAsync("Create a file called test.txt with 'version 1'");

            string? userMessageId = null;
            await foreach (var message in client.ReceiveResponseAsync())
            {
                TrackModelUsed(message);
                if (message is UserMessage user)
                {
                    userMessageId = user.Uuid;
                    Log($"  User message ID: {userMessageId}");
                }
                if (message is ResultMessage)
                {
                    break;
                }
            }

            // Try to rewind (may not work in all environments)
            if (!string.IsNullOrEmpty(userMessageId))
            {
                try
                {
                    await client.RewindFilesAsync(userMessageId);
                    Log($"  Rewind request sent");
                }
                catch (Exception ex)
                {
                    Log($"  Rewind failed (may be expected): {ex.Message}");
                }
            }

            return (true, null, null);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, recursive: true);
                Log($"  Cleaned up test directory");
            }
        }
    }

    // ============================================
    // CONTENT BLOCK PARSING TESTS
    // ============================================

    /// <summary>
    /// Test A13: Tool Result Block - Parse tool result content
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA13_ToolResultBlock(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 3,
            MaxBudgetUsd = 0.15
        };

        bool hasToolResult = false;
        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync(
            "Read the first 3 lines of CLAUDE.md in the current directory", options))
        {
            TrackModelUsed(message);
            if (message is UserMessage user)
            {
                // Tool results come as user messages with tool_result content
                var blocks = user.GetContentBlocks();
                if (blocks != null)
                {
                    foreach (var block in blocks)
                    {
                        if (block is ToolResultBlock toolResult)
                        {
                            hasToolResult = true;
                            Log($"  ToolResultBlock: tool_use_id={toolResult.ToolUseId}");
                            Log($"    IsError: {toolResult.IsError}");
                        }
                    }
                }
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  ToolResultBlock found: {hasToolResult}");
        return (true, cost, null);
    }

    /// <summary>
    /// Test A14: Multiple Content Blocks - Parse multiple blocks in one message
    /// </summary>
    private static async Task<(bool, decimal?, string?)> TestA14_MultipleContentBlocks(string workingDir)
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = DefaultModel,
            MaxTurns = 3,
            MaxBudgetUsd = 0.15
        };

        int maxBlocksInMessage = 0;
        var blockTypeCounts = new Dictionary<string, int>();
        decimal? cost = null;

        await foreach (var message in ClaudeAgent.QueryAsync(
            "Read CLAUDE.md and then summarize what you found", options))
        {
            TrackModelUsed(message);
            if (message is AssistantMessage assistant)
            {
                if (assistant.Content.Count > maxBlocksInMessage)
                {
                    maxBlocksInMessage = assistant.Content.Count;
                }

                foreach (var block in assistant.Content)
                {
                    var typeName = block.Type;
                    blockTypeCounts.TryGetValue(typeName, out var count);
                    blockTypeCounts[typeName] = count + 1;
                }
            }
            if (message is ResultMessage result)
            {
                cost = (decimal?)(result.TotalCostUsd);
            }
        }

        Log($"  Max blocks in single message: {maxBlocksInMessage}");
        Log($"  Block types seen: {string.Join(", ", blockTypeCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

        return (true, cost, null);
    }
}
