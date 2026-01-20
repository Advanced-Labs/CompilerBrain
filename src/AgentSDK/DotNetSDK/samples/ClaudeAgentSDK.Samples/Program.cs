using System.Text.Json;
using ClaudeAgentSDK;
using ClaudeAgentSDK.Models;
using ClaudeAgentSDK.Samples;

// ============================================
// CLAUDE AGENT SDK - SAMPLE AND TEST RUNNER
// ============================================

var workingDir = Environment.CurrentDirectory;
var logFile = Path.Combine(workingDir, "claude-sdk-debug.log");

// Simple logging helper
void Log(string message, bool alsoConsole = true)
{
    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
    var line = $"[{timestamp}] {message}";
    File.AppendAllText(logFile, line + Environment.NewLine);
    if (alsoConsole)
    {
        Console.WriteLine(line);
    }
}

// Clear previous log
if (File.Exists(logFile))
{
    File.Delete(logFile);
}

// Parse command-line arguments
var runMode = args.Length > 0 ? args[0].ToLower() : "menu";

Console.WriteLine("============================================");
Console.WriteLine("Claude Agent SDK - Sample and Test Runner");
Console.WriteLine("============================================");
Console.WriteLine($"Working directory: {workingDir}");
Console.WriteLine($"Log file: {logFile}");
Console.WriteLine();

switch (runMode)
{
    case "basic":
        await RunBasicTests();
        break;
    case "features":
        await RunFeatureTests();
        break;
    case "advanced":
        await RunAdvancedTests();
        break;
    case "all":
        await RunBasicTests();
        Console.WriteLine("\n\n");
        await RunFeatureTests();
        Console.WriteLine("\n\n");
        await RunAdvancedTests();
        break;
    case "quick":
        await RunQuickSmokeTest();
        break;
    default:
        ShowMenu();
        break;
}

void ShowMenu()
{
    Console.WriteLine("Usage: dotnet run [mode]");
    Console.WriteLine();
    Console.WriteLine("Modes:");
    Console.WriteLine("  basic     - Run basic diagnostic tests (Tests 0-3)");
    Console.WriteLine("  features  - Run comprehensive feature tests (Tests 4-25)");
    Console.WriteLine("  advanced  - Run advanced feature tests (Tests A1-A14)");
    Console.WriteLine("  all       - Run all tests (basic + features + advanced)");
    Console.WriteLine("  quick     - Quick smoke test (minimal API calls)");
    Console.WriteLine("  menu      - Show this menu (default)");
    Console.WriteLine();
    Console.WriteLine("Estimated costs:");
    Console.WriteLine("  quick     ~$0.05");
    Console.WriteLine("  basic     ~$0.15");
    Console.WriteLine("  features  ~$0.65");
    Console.WriteLine("  advanced  ~$1.50");
    Console.WriteLine("  all       ~$2.30");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run features");
}

async Task RunQuickSmokeTest()
{
    Log("=== Quick Smoke Test ===");
    Log($"Using model: {FeatureTests.DefaultModel}");
    Log("Testing basic connectivity and SDK functionality...");

    try
    {
        // Test 1: CLI exists
        var cliPath = FindCli();
        Log($"CLI found: {cliPath}");

        // Test 2: Simple query
        Log("Running simple query...");
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = FeatureTests.DefaultModel,
            MaxTurns = 1,
            MaxBudgetUsd = 0.05
        };

        int messageCount = 0;
        await foreach (var message in ClaudeAgent.QueryAsync("Say 'SDK OK'", options))
        {
            messageCount++;
            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    if (block is TextBlock text)
                    {
                        Log($"Response: {text.Text}");
                    }
                }
            }
            if (message is ResultMessage result)
            {
                Log($"Cost: ${result.TotalCostUsd:F4}");
            }
        }

        Log($"Messages received: {messageCount}");
        Log("SMOKE TEST PASSED");
    }
    catch (Exception ex)
    {
        Log($"SMOKE TEST FAILED: {ex.Message}");
    }
}

async Task RunBasicTests()
{
    Log($"=== Claude Agent SDK Diagnostic Sample ===");
    Log($"Working directory: {workingDir}");
    Log($"Log file: {logFile}");
    Log($"Default model: {FeatureTests.DefaultModel}");
    Log($"");

    // Stderr callback to capture CLI debug output
    void OnStderr(string line)
    {
        Log($"[STDERR] {line}");
    }

    // ============================================
    // Test 0: Raw CLI test - see what the CLI actually outputs
    // ============================================
    Log("=== Test 0: Raw CLI Output Test ===");
    try
    {
        var cliPath = FindCli();
        Log($"Running CLI directly with --output-format stream-json");

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            }
        };
        process.StartInfo.ArgumentList.Add("--output-format");
        process.StartInfo.ArgumentList.Add("stream-json");
        process.StartInfo.ArgumentList.Add("--verbose");
        process.StartInfo.ArgumentList.Add("--print");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add("What is 2+2? Reply with just the number.");

        Log($"Command: {cliPath} {string.Join(" ", process.StartInfo.ArgumentList)}");

        process.Start();

        // Read stdout and stderr
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Log($"Exit code: {process.ExitCode}");
        Log($"STDOUT ({stdout.Length} chars):");
        var lines = stdout.Split('\n');
        foreach (var line in lines.Take(20))
        {
            if (!string.IsNullOrWhiteSpace(line))
                Log($"  {line.TrimEnd()}");
        }
        if (lines.Length > 20)
            Log($"  ... ({lines.Length - 20} more lines)");

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Log($"STDERR ({stderr.Length} chars):");
            foreach (var line in stderr.Split('\n').Take(10))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Log($"  {line.TrimEnd()}");
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Test 0 FAILED: {ex.GetType().Name}: {ex.Message}");
    }

    Log("");

    // ============================================
    // Test 1: Simple query with verbose output
    // ============================================
    Log("=== Test 1: Simple One-Shot Query ===");
    try
    {
        var options = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = FeatureTests.DefaultModel,
            StderrCallback = OnStderr
        };

        Log("Starting query: 'What is 2 + 2?'");

        int messageCount = 0;
        await foreach (var message in ClaudeAgent.QueryAsync("What is 2 + 2? Reply briefly.", options))
        {
            messageCount++;
            Log($"Received message #{messageCount}: Type={message.Type}");

            switch (message)
            {
                case AssistantMessage assistant:
                    Log($"  Model: {assistant.Model}");
                    foreach (var block in assistant.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Log($"  Text: {text.Text}");
                        }
                        else if (block is ToolUseBlock tool)
                        {
                            Log($"  Tool: {tool.Name} (id={tool.Id})");
                        }
                    }
                    break;

                case UserMessage user:
                    var content = user.GetContentAsString();
                    if (content != null)
                    {
                        Log($"  Content: {content}");
                    }
                    else
                    {
                        Log($"  Content blocks: {user.GetContentBlocks()?.Count ?? 0}");
                    }
                    break;

                case SystemMessage system:
                    Log($"  Subtype: {system.Subtype}");
                    break;

                case ResultMessage result:
                    Log($"  Result: SessionId={result.SessionId}, Turns={result.NumTurns}, Duration={result.DurationMs}ms");
                    Log($"  IsError={result.IsError}, Cost=${result.TotalCostUsd:F4}");
                    if (!string.IsNullOrEmpty(result.Result))
                    {
                        Log($"  Result text: {result.Result}");
                    }
                    break;

                default:
                    Log($"  (Unknown message type: {message.GetType().Name})");
                    break;
            }
        }

        Log($"Test 1 complete. Total messages received: {messageCount}");
    }
    catch (Exception ex)
    {
        Log($"Test 1 FAILED: {ex.GetType().Name}: {ex.Message}");
        Log($"Stack trace: {ex.StackTrace}", alsoConsole: false);
    }

    Log("");

    // ============================================
    // Test 2: Check if CLI is reachable
    // ============================================
    Log("=== Test 2: CLI Reachability Check ===");
    try
    {
        // Try to find the CLI
        var cliPath = FindCli();
        Log($"CLI found at: {cliPath}");

        // Try running --version
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Log($"CLI version output: {stdout.Trim()}");
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Log($"CLI stderr: {stderr.Trim()}");
        }
        Log($"CLI exit code: {process.ExitCode}");
    }
    catch (Exception ex)
    {
        Log($"Test 2 FAILED: {ex.GetType().Name}: {ex.Message}");
    }

    Log("");

    // ============================================
    // Test 3: Interactive client (streaming mode)
    // ============================================
    Log("=== Test 3: Interactive Client (Streaming Mode) ===");
    try
    {
        var clientOptions = new ClaudeAgentOptions
        {
            WorkingDirectory = workingDir,
            Model = FeatureTests.DefaultModel,
            StderrCallback = OnStderr
        };

        Log("Creating client...");
        await using var client = ClaudeAgent.CreateClient(clientOptions);

        Log("Connecting with prompt: 'Say hello briefly'");
        await client.ConnectAsync("Say hello briefly");

        Log("Receiving response...");
        int msgCount = 0;
        await foreach (var message in client.ReceiveResponseAsync())
        {
            msgCount++;
            Log($"Received message #{msgCount}: Type={message.Type}");

            if (message is AssistantMessage assistant)
            {
                foreach (var block in assistant.Content)
                {
                    if (block is TextBlock text)
                    {
                        Log($"  Text: {text.Text}");
                    }
                }
            }
            else if (message is ResultMessage result)
            {
                Log($"  Result: {result.SessionId}, {result.NumTurns} turns");
            }
        }

        Log($"Test 3 complete. Messages received: {msgCount}");
    }
    catch (Exception ex)
    {
        Log($"Test 3 FAILED: {ex.GetType().Name}: {ex.Message}");
        Log($"Stack trace:", alsoConsole: false);
        Log(ex.StackTrace ?? "", alsoConsole: false);
    }

    Log("");
    Log("=== Basic tests complete ===");
    Log($"See {logFile} for full debug output");
}

async Task RunFeatureTests()
{
    Log("Starting comprehensive feature tests...");
    Log("");

    var results = await FeatureTests.RunAllTestsAsync(workingDir, Log);

    Log("");
    Log("=== Feature tests complete ===");
    Log($"See {logFile} for full debug output");

    // Return non-zero exit code if any tests failed
    var failed = results.Count(r => !r.Passed);
    if (failed > 0)
    {
        Log($"\nWARNING: {failed} test(s) failed");
        Environment.ExitCode = 1;
    }
}

async Task RunAdvancedTests()
{
    Log("Starting advanced feature tests...");
    Log("Note: These tests may take longer and consume more API credits.");
    Log("");

    var results = await AdvancedFeatureTests.RunAllTestsAsync(workingDir, Log);

    Log("");
    Log("=== Advanced tests complete ===");
    Log($"See {logFile} for full debug output");

    // Return non-zero exit code if any tests failed
    var failed = results.Count(r => !r.Passed);
    if (failed > 0)
    {
        Log($"\nWARNING: {failed} test(s) failed");
        Environment.ExitCode = 1;
    }
}

// Helper to find CLI (mirrors SDK logic)
static string FindCli()
{
    // Check PATH
    var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
    var exeName = OperatingSystem.IsWindows() ? "claude.exe" : "claude";

    foreach (var dir in pathDirs)
    {
        var fullPath = Path.Combine(dir, exeName);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }
    }

    // Check common locations
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var commonPaths = new[]
    {
        Path.Combine(home, ".npm-global", "bin", exeName),
        Path.Combine(home, "AppData", "Roaming", "npm", exeName),
        "/usr/local/bin/claude",
        Path.Combine(home, ".local", "bin", "claude"),
    };

    foreach (var path in commonPaths)
    {
        if (File.Exists(path))
        {
            return path;
        }
    }

    throw new FileNotFoundException($"Claude CLI not found. Searched PATH and common locations.");
}
