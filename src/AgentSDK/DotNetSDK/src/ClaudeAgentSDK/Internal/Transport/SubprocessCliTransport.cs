using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSDK.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSDK.Internal.Transport;

/// <summary>
/// Transport implementation that uses a subprocess to communicate with Claude Code CLI.
/// Uses ProcessX for async process management.
/// </summary>
public sealed class SubprocessCliTransport : ITransport
{
    private readonly ClaudeAgentOptions? _options;
    private readonly string? _initialPrompt;
    private readonly bool _isStreamingMode;
    private readonly ILogger? _logger;
    private readonly int _maxBufferSize;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly List<string> _tempFiles = [];
    private bool _isReady;
    private bool _isDisposed;
    private Task? _stderrTask;
    private CancellationTokenSource? _stderrCts;
    private Exception? _exitError;

    /// <inheritdoc />
    public bool IsReady => _isReady && _process is { HasExited: false };

    /// <summary>
    /// Creates a new subprocess CLI transport.
    /// </summary>
    /// <param name="prompt">The initial prompt (for non-streaming mode).</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="isStreamingMode">Whether to use streaming mode.</param>
    /// <param name="logger">Optional logger.</param>
    public SubprocessCliTransport(
        string? prompt = null,
        ClaudeAgentOptions? options = null,
        bool isStreamingMode = false,
        ILogger? logger = null)
    {
        _initialPrompt = prompt;
        _options = options;
        _isStreamingMode = isStreamingMode;
        _logger = logger;
        _maxBufferSize = options?.MaxBufferSize ?? 10 * 1024 * 1024; // 10MB default
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isReady)
            throw new ClaudeAgentSDK.InvalidOperationException("Transport is already connected");

        var cliPath = FindCli();
        var args = BuildCommandArgs();

        _logger?.LogDebug("Starting Claude CLI: {CliPath} {Args}", cliPath, string.Join(" ", args));

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            RedirectStandardInput = _isStreamingMode,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Set working directory
        if (!string.IsNullOrEmpty(_options?.WorkingDirectory))
        {
            if (!Directory.Exists(_options.WorkingDirectory))
            {
                throw new CliConnectionException($"Working directory does not exist: {_options.WorkingDirectory}");
            }
            startInfo.WorkingDirectory = _options.WorkingDirectory;
        }

        // Set environment variables
        if (_options?.Environment is { Count: > 0 })
        {
            foreach (var (key, value) in _options.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            _process = Process.Start(startInfo);
            if (_process == null)
            {
                throw new CliConnectionException("Failed to start Claude CLI process");
            }

            if (_isStreamingMode)
            {
                _stdin = _process.StandardInput;
            }
            _stdout = _process.StandardOutput;
            _stderr = _process.StandardError;

            // Start stderr reading task
            _stderrCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _stderrTask = ReadStderrAsync(_stderrCts.Token);

            _isReady = true;
            _logger?.LogDebug("Claude CLI process started successfully (PID: {ProcessId})", _process.Id);
        }
        catch (Exception ex) when (ex is not ClaudeSDKException)
        {
            throw new CliConnectionException($"Failed to start Claude CLI: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isStreamingMode)
            throw new ClaudeAgentSDK.InvalidOperationException("Cannot write in non-streaming mode");

        if (_stdin == null)
            throw new NotConnectedException("Transport is not connected");

        if (_exitError != null)
            throw new CliConnectionException("Process has exited", _exitError);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _stdin.WriteLineAsync(data.AsMemory(), cancellationToken);
            await _stdin.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not ClaudeSDKException)
        {
            _exitError = ex;
            throw new CliConnectionException($"Failed to write to CLI: {ex.Message}", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_stdout == null)
            throw new NotConnectedException("Transport is not connected");

        var buffer = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _stdout.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading from stdout");
                _exitError = ex;
                throw new CliConnectionException($"Error reading from CLI: {ex.Message}", ex);
            }

            if (line == null)
            {
                // End of stream
                if (buffer.Length > 0)
                {
                    // Try to parse remaining buffer
                    var remaining = buffer.ToString().Trim();
                    if (!string.IsNullOrEmpty(remaining))
                    {
                        if (TryParseJson(remaining, out var finalData))
                        {
                            yield return finalData!;
                        }
                        else
                        {
                            _logger?.LogWarning("Unparsed data remaining in buffer: {Data}", remaining);
                        }
                    }
                }
                break;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Try to parse as JSON
            buffer.Append(line);
            var content = buffer.ToString();

            if (TryParseJson(content, out var data))
            {
                buffer.Clear();
                yield return data!;
            }
            else if (buffer.Length > _maxBufferSize)
            {
                throw new CliJsonDecodeException(
                    $"JSON buffer exceeded maximum size ({_maxBufferSize} bytes)",
                    content);
            }
            // If not valid JSON yet, continue accumulating
        }

        // Check for process exit
        if (_process != null && _process.HasExited && _process.ExitCode != 0)
        {
            var stderrContent = GetStderrContent();
            throw new ProcessException(
                $"CLI process exited with code {_process.ExitCode}",
                _process.ExitCode,
                stderrContent);
        }
    }

    /// <inheritdoc />
    public async Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        if (_stdin != null)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                _stdin.Close();
                _stdin = null;
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await DisposeAsyncCore();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    private async Task DisposeAsyncCore()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _isReady = false;

        // Cancel stderr reading
        _stderrCts?.Cancel();
        if (_stderrTask != null)
        {
            try
            {
                await _stderrTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore cancellation/timeout
            }
        }
        _stderrCts?.Dispose();

        // Close streams
        _stdin?.Dispose();
        _stdout?.Dispose();
        _stderr?.Dispose();

        // Terminate process
        if (_process != null)
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
            _process.Dispose();
        }

        // Clean up temp files
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _writeLock.Dispose();
    }

    private string FindCli()
    {
        // Check custom path first
        if (!string.IsNullOrEmpty(_options?.CliPath))
        {
            if (File.Exists(_options.CliPath))
                return _options.CliPath;
            throw new CliNotFoundException($"CLI not found at specified path: {_options.CliPath}");
        }

        // Check common locations
        var possiblePaths = new List<string>();

        // System PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            possiblePaths.Add(Path.Combine(dir, GetCliExecutableName()));
        }

        // Common installation paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        possiblePaths.AddRange([
            Path.Combine(home, ".npm-global", "bin", GetCliExecutableName()),
            "/usr/local/bin/claude",
            Path.Combine(home, ".local", "bin", "claude"),
            Path.Combine(home, "node_modules", ".bin", GetCliExecutableName()),
            Path.Combine(home, ".yarn", "bin", "claude"),
            Path.Combine(home, ".claude", "local", "claude")
        ]);

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogDebug("Found Claude CLI at: {Path}", path);
                return path;
            }
        }

        throw new CliNotFoundException();
    }

    private static string GetCliExecutableName()
    {
        return OperatingSystem.IsWindows() ? "claude.exe" : "claude";
    }

    private List<string> BuildCommandArgs()
    {
        var args = new List<string>
        {
            "--output-format", "stream-json",
            "--verbose"
        };

        // System prompt
        if (!string.IsNullOrEmpty(_options?.SystemPrompt))
        {
            args.Add("--system-prompt");
            args.Add(_options.SystemPrompt);
        }

        // Tools configuration
        if (_options?.ToolsPreset != null)
        {
            args.Add("--tools");
            args.Add(_options.ToolsPreset.Value.ToString().ToLowerInvariant());
        }
        else if (_options?.Tools is { Count: > 0 })
        {
            args.Add("--tools");
            args.Add(string.Join(",", _options.Tools));
        }

        if (_options?.AllowedTools is { Count: > 0 })
        {
            args.Add("--allowedTools");
            args.Add(string.Join(",", _options.AllowedTools));
        }

        if (_options?.DisallowedTools is { Count: > 0 })
        {
            args.Add("--disallowedTools");
            args.Add(string.Join(",", _options.DisallowedTools));
        }

        // Model configuration
        if (!string.IsNullOrEmpty(_options?.Model))
        {
            args.Add("--model");
            args.Add(_options.Model);
        }

        if (!string.IsNullOrEmpty(_options?.FallbackModel))
        {
            args.Add("--fallback-model");
            args.Add(_options.FallbackModel);
        }

        // Limits
        if (_options?.MaxTurns != null)
        {
            args.Add("--max-turns");
            args.Add(_options.MaxTurns.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_options?.MaxBudgetUsd != null)
        {
            args.Add("--max-budget-usd");
            args.Add(_options.MaxBudgetUsd.Value.ToString("F2", CultureInfo.InvariantCulture));
        }

        if (_options?.MaxThinkingTokens != null)
        {
            args.Add("--max-thinking-tokens");
            args.Add(_options.MaxThinkingTokens.Value.ToString(CultureInfo.InvariantCulture));
        }

        // Permission mode
        if (_options?.PermissionMode != null)
        {
            var mode = _options.PermissionMode.Value switch
            {
                PermissionMode.Default => "default",
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.BypassPermissions => "bypassPermissions",
                _ => "default"
            };
            args.Add("--permission-mode");
            args.Add(mode);
        }

        if (!string.IsNullOrEmpty(_options?.PermissionPromptToolName))
        {
            args.Add("--permission-prompt-tool");
            args.Add(_options.PermissionPromptToolName);
        }

        // Session control
        if (_options?.ContinueConversation == true)
        {
            args.Add("--continue");
        }

        if (!string.IsNullOrEmpty(_options?.Resume))
        {
            args.Add("--resume");
            args.Add(_options.Resume);
        }

        if (_options?.ForkSession == true)
        {
            args.Add("--fork-session");
        }

        // MCP configuration
        if (!string.IsNullOrEmpty(_options?.McpConfigPath))
        {
            args.Add("--mcp-config");
            args.Add(_options.McpConfigPath);
        }
        else if (_options?.McpServers is { Count: > 0 })
        {
            var json = JsonSerializer.Serialize(_options.McpServers);
            if (json.Length > 1000)
            {
                // Write to temp file for long configs
                var tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, json);
                _tempFiles.Add(tempFile);
                args.Add("--mcp-config");
                args.Add(tempFile);
            }
            else
            {
                args.Add("--mcp-config");
                args.Add(json);
            }
        }

        // Settings
        if (!string.IsNullOrEmpty(_options?.Settings))
        {
            args.Add("--settings");
            args.Add(_options.Settings);
        }

        // Additional directories
        if (_options?.AddDirs is { Count: > 0 })
        {
            foreach (var dir in _options.AddDirs)
            {
                args.Add("--add-dir");
                args.Add(dir);
            }
        }

        // Betas
        if (_options?.Betas is { Count: > 0 })
        {
            args.Add("--betas");
            args.Add(string.Join(",", _options.Betas));
        }

        // Agents
        if (_options?.Agents is { Count: > 0 })
        {
            var json = JsonSerializer.Serialize(_options.Agents);
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, json);
            _tempFiles.Add(tempFile);
            args.Add("--agents");
            args.Add(tempFile);
        }

        // Plugins
        if (_options?.Plugins is { Count: > 0 })
        {
            foreach (var plugin in _options.Plugins)
            {
                args.Add("--plugin-dir");
                args.Add(plugin.Path);
            }
        }

        // Sandbox
        if (_options?.Sandbox != null)
        {
            var sandboxJson = JsonSerializer.Serialize(_options.Sandbox);
            args.Add("--sandbox");
            args.Add(sandboxJson);
        }

        // Output format
        if (_options?.OutputFormat is { Count: > 0 })
        {
            var schemaJson = JsonSerializer.Serialize(_options.OutputFormat);
            args.Add("--json-schema");
            args.Add(schemaJson);
        }

        // Include partial messages
        if (_options?.IncludePartialMessages == true)
        {
            args.Add("--include-partial-messages");
        }

        // File checkpointing
        if (_options?.EnableFileCheckpointing == true)
        {
            args.Add("--enable-file-checkpointing");
        }

        // Input mode
        if (_isStreamingMode)
        {
            args.Add("--input-format");
            args.Add("stream-json");

            // Add permission prompt tool for SDK control protocol
            if (_options?.CanUseTool != null || _options?.Hooks != null)
            {
                args.Add("--permission-prompt-tool");
                args.Add("stdio");
            }
        }
        else if (!string.IsNullOrEmpty(_initialPrompt))
        {
            args.Add("--print");
            args.Add("--");
            args.Add(_initialPrompt);
        }

        return args;
    }

    private async Task ReadStderrAsync(CancellationToken cancellationToken)
    {
        if (_stderr == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _stderr.ReadLineAsync(cancellationToken);
                if (line == null) break;

                _logger?.LogDebug("[STDERR] {Line}", line);
                _options?.StderrCallback?.Invoke(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading stderr");
        }
    }

    private static string GetStderrContent()
    {
        // Note: In the current implementation, stderr is read line-by-line
        // and not accumulated. If needed, we could store it.
        return "";
    }

    private static bool TryParseJson(string content, out Dictionary<string, object?>? result)
    {
        result = null;
        try
        {
            result = JsonSerializer.Deserialize<Dictionary<string, object?>>(content);
            return result != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
