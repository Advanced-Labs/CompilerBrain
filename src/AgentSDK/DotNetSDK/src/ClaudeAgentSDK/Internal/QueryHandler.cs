using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSDK.Internal.Transport;
using ClaudeAgentSDK.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSDK.Internal;

/// <summary>
/// Handles the SDK control protocol for bidirectional communication with Claude Code CLI.
/// </summary>
public sealed class QueryHandler : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ITransport _transport;
    private readonly bool _isStreamingMode;
    private readonly Func<ToolPermissionRequest, Task<PermissionResult>>? _canUseTool;
    private readonly Dictionary<HookEvent, List<HookMatcher>>? _hooks;
    private readonly ILogger? _logger;
    private readonly TimeSpan _initializeTimeout;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object?>>> _pendingRequests = new();
    private readonly Channel<Dictionary<string, object?>> _messageChannel;
    private readonly CancellationTokenSource _cts = new();

    private Task? _readTask;
    private int _requestCounter;
    private bool _isDisposed;
    private Dictionary<string, object?>? _serverInfo;

    /// <summary>
    /// Gets the server initialization info (streaming mode only).
    /// </summary>
    public Dictionary<string, object?>? ServerInfo => _serverInfo;

    /// <summary>
    /// Creates a new QueryHandler instance.
    /// </summary>
    public QueryHandler(
        ITransport transport,
        bool isStreamingMode = false,
        Func<ToolPermissionRequest, Task<PermissionResult>>? canUseTool = null,
        Dictionary<HookEvent, List<HookMatcher>>? hooks = null,
        TimeSpan? initializeTimeout = null,
        ILogger? logger = null)
    {
        _transport = transport;
        _isStreamingMode = isStreamingMode;
        _canUseTool = canUseTool;
        _hooks = hooks;
        _initializeTimeout = initializeTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;

        _messageChannel = Channel.CreateUnbounded<Dictionary<string, object?>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Starts the message reading background task.
    /// </summary>
    public void Start()
    {
        if (_readTask != null)
            throw new ClaudeAgentSDK.InvalidOperationException("QueryHandler is already started");

        _readTask = ReadMessagesLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Gets whether the control protocol is enabled (has callbacks that require it).
    /// </summary>
    public bool IsControlProtocolEnabled => _canUseTool != null || (_hooks != null && _hooks.Count > 0);

    /// <summary>
    /// Initializes the SDK control protocol (streaming mode only, when control protocol is enabled).
    /// </summary>
    public async Task<Dictionary<string, object?>?> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Only initialize if we're in streaming mode AND control protocol is enabled
        if (!_isStreamingMode || !IsControlProtocolEnabled)
            return null;

        var request = new InitializeRequest { ProtocolVersion = 1 };
        var response = await SendControlRequestAsync(request, _initializeTimeout, cancellationToken);
        _serverInfo = response;
        return response;
    }

    /// <summary>
    /// Receives parsed messages from the CLI.
    /// </summary>
    public async IAsyncEnumerable<Dictionary<string, object?>> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            Dictionary<string, object?>? message;
            try
            {
                message = await _messageChannel.Reader.ReadAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (ChannelClosedException)
            {
                yield break;
            }

            if (message != null)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Streams input to the CLI (streaming mode only).
    /// </summary>
    public async Task StreamInputAsync(IAsyncEnumerable<Dictionary<string, object?>> input, CancellationToken cancellationToken = default)
    {
        if (!_isStreamingMode)
            throw new ClaudeAgentSDK.InvalidOperationException("Cannot stream input in non-streaming mode");

        await foreach (var message in input.WithCancellation(cancellationToken))
        {
            var json = JsonSerializer.Serialize(message, SnakeCaseJsonOptions);
            await _transport.WriteAsync(json, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a user query message.
    /// </summary>
    public async Task SendQueryAsync(string prompt, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        // CLI expects nested message structure: {"type":"user","message":{"role":"user","content":"..."}}
        var message = new Dictionary<string, object?>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = prompt
            }
        };

        if (!string.IsNullOrEmpty(sessionId))
        {
            message["session_id"] = sessionId;
        }

        var json = JsonSerializer.Serialize(message, SnakeCaseJsonOptions);
        await _transport.WriteAsync(json, cancellationToken);
    }

    /// <summary>
    /// Sends an interrupt signal.
    /// </summary>
    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        var request = new InterruptRequest();
        await SendControlRequestAsync(request, TimeSpan.FromSeconds(10), cancellationToken);
    }

    /// <summary>
    /// Sets the permission mode.
    /// </summary>
    public async Task SetPermissionModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        var request = new SetPermissionModeRequest { Mode = mode };
        await SendControlRequestAsync(request, TimeSpan.FromSeconds(10), cancellationToken);
    }

    /// <summary>
    /// Sets the model.
    /// </summary>
    public async Task SetModelAsync(string? model, CancellationToken cancellationToken = default)
    {
        var request = new SetModelRequest { Model = model };
        await SendControlRequestAsync(request, TimeSpan.FromSeconds(10), cancellationToken);
    }

    /// <summary>
    /// Rewinds files to a specific checkpoint.
    /// </summary>
    public async Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default)
    {
        var request = new RewindFilesRequest { UserMessageId = userMessageId };
        await SendControlRequestAsync(request, TimeSpan.FromSeconds(30), cancellationToken);
    }

    /// <summary>
    /// Closes the query handler.
    /// </summary>
    public async Task CloseAsync()
    {
        await DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();

        _messageChannel.Writer.TryComplete();

        if (_readTask != null)
        {
            try
            {
                await _readTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        // Complete all pending requests with cancellation
        foreach (var pending in _pendingRequests.Values)
        {
            pending.TrySetCanceled();
        }
        _pendingRequests.Clear();

        _cts.Dispose();
    }

    private async Task ReadMessagesLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var rawMessage in _transport.ReadMessagesAsync(cancellationToken))
            {
                await HandleIncomingMessageAsync(rawMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in message read loop");
        }
        finally
        {
            _messageChannel.Writer.TryComplete();
        }
    }

    private async Task HandleIncomingMessageAsync(Dictionary<string, object?> message, CancellationToken cancellationToken)
    {
        // Check if it's a control request from CLI
        if (MessageParser.IsControlRequest(message))
        {
            await HandleControlRequestAsync(message, cancellationToken);
            return;
        }

        // Check if it's a control response for our requests
        if (MessageParser.IsControlResponse(message))
        {
            HandleControlResponse(message);
            return;
        }

        // Regular message - queue for processing
        await _messageChannel.Writer.WriteAsync(message, cancellationToken);
    }

    private async Task HandleControlRequestAsync(Dictionary<string, object?> message, CancellationToken cancellationToken)
    {
        var requestId = GetString(message, "request_id") ?? "";
        var request = GetDict(message, "request");

        if (request == null)
        {
            _logger?.LogWarning("Control request missing 'request' field");
            return;
        }

        var subtype = GetString(request, "subtype") ?? "";

        try
        {
            Dictionary<string, object?>? response = subtype switch
            {
                "can_use_tool" => await HandleCanUseToolAsync(request, cancellationToken),
                "hook_callback" => await HandleHookCallbackAsync(request, cancellationToken),
                "mcp_message" => await HandleMcpMessageAsync(request, cancellationToken),
                _ => null
            };

            await SendControlResponseAsync(requestId, response, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling control request {Subtype}", subtype);
            await SendControlResponseAsync(requestId, null, ex.Message, cancellationToken);
        }
    }

    private async Task<Dictionary<string, object?>?> HandleCanUseToolAsync(
        Dictionary<string, object?> request,
        CancellationToken cancellationToken)
    {
        if (_canUseTool == null)
        {
            // No callback - allow by default
            return new Dictionary<string, object?> { ["behavior"] = "allow" };
        }

        var toolName = GetString(request, "tool_name") ?? "";
        var input = GetDict(request, "input") ?? [];
        var contextData = GetDict(request, "context");

        var context = new ToolPermissionContext
        {
            Cwd = GetString(contextData, "cwd") ?? "",
            SessionId = GetString(contextData, "session_id") ?? "",
            ReadablePaths = GetStringList(contextData, "readable_paths"),
            WritablePaths = GetStringList(contextData, "writable_paths")
        };

        var permissionRequest = new ToolPermissionRequest
        {
            ToolName = toolName,
            Input = input,
            Context = context
        };

        var result = await _canUseTool(permissionRequest);

        return result switch
        {
            PermissionResultAllow allow => new Dictionary<string, object?>
            {
                ["behavior"] = "allow",
                ["updatedInput"] = allow.UpdatedInput,
                ["updatedPermissions"] = allow.UpdatedPermissions
            },
            PermissionResultDeny deny => new Dictionary<string, object?>
            {
                ["behavior"] = "deny",
                ["message"] = deny.Message,
                ["interrupt"] = deny.Interrupt
            },
            _ => new Dictionary<string, object?> { ["behavior"] = "allow" }
        };
    }

    private async Task<Dictionary<string, object?>?> HandleHookCallbackAsync(
        Dictionary<string, object?> request,
        CancellationToken cancellationToken)
    {
        if (_hooks == null || _hooks.Count == 0)
        {
            return new Dictionary<string, object?> { ["continue"] = true };
        }

        var hookEventStr = GetString(request, "hook_event") ?? "";
        if (!Enum.TryParse<HookEvent>(hookEventStr, ignoreCase: true, out var hookEvent))
        {
            return new Dictionary<string, object?> { ["continue"] = true };
        }

        if (!_hooks.TryGetValue(hookEvent, out var matchers) || matchers.Count == 0)
        {
            return new Dictionary<string, object?> { ["continue"] = true };
        }

        var matcher = GetString(request, "matcher");
        var inputData = GetDict(request, "input") ?? [];
        var contextData = GetDict(request, "context");

        var context = new HookContext
        {
            Cwd = GetString(contextData, "cwd") ?? "",
            SessionId = GetString(contextData, "session_id") ?? ""
        };

        // Create the appropriate hook input based on event type
        HookInput? hookInput = hookEvent switch
        {
            HookEvent.PreToolUse => new PreToolUseHookInput
            {
                ToolName = GetString(inputData, "tool_name") ?? "",
                ToolInput = GetDict(inputData, "tool_input") ?? []
            },
            HookEvent.PostToolUse => new PostToolUseHookInput
            {
                ToolName = GetString(inputData, "tool_name") ?? "",
                ToolInput = GetDict(inputData, "tool_input") ?? [],
                ToolOutput = inputData.GetValueOrDefault("tool_output"),
                IsError = GetBool(inputData, "is_error") ?? false
            },
            HookEvent.UserPromptSubmit => new UserPromptSubmitHookInput
            {
                Prompt = GetString(inputData, "prompt") ?? ""
            },
            HookEvent.Stop => new StopHookInput
            {
                StopReason = GetString(inputData, "stop_reason")
            },
            HookEvent.SubagentStop => new SubagentStopHookInput
            {
                AgentName = GetString(inputData, "agent_name") ?? "",
                StopReason = GetString(inputData, "stop_reason")
            },
            HookEvent.PreCompact => new PreCompactHookInput
            {
                ContextSize = GetInt(inputData, "context_size") ?? 0
            },
            _ => null
        };

        if (hookInput == null)
        {
            return new Dictionary<string, object?> { ["continue"] = true };
        }

        // Find matching hooks and execute them
        foreach (var hookMatcher in matchers)
        {
            // Check if matcher matches
            if (!string.IsNullOrEmpty(hookMatcher.Matcher) && hookMatcher.Matcher != matcher)
            {
                continue;
            }

            foreach (var callback in hookMatcher.Hooks)
            {
                var output = await callback.Handler(hookInput, matcher, context);

                // If hook says don't continue, return immediately
                if (!output.Continue)
                {
                    return new Dictionary<string, object?>
                    {
                        ["continue"] = false,
                        ["suppressOutput"] = output.SuppressOutput,
                        ["stopReason"] = output.StopReason,
                        ["decision"] = output.Decision,
                        ["systemMessage"] = output.SystemMessage,
                        ["reason"] = output.Reason,
                        ["hookSpecificOutput"] = output.HookSpecificOutput
                    };
                }
            }
        }

        return new Dictionary<string, object?> { ["continue"] = true };
    }

    private Task<Dictionary<string, object?>?> HandleMcpMessageAsync(
        Dictionary<string, object?> request,
        CancellationToken cancellationToken)
    {
        // MCP server handling would go here if SDK MCP servers are supported
        // For now, return null to indicate no handling
        _logger?.LogWarning("MCP message handling not implemented");
        return Task.FromResult<Dictionary<string, object?>?>(null);
    }

    private void HandleControlResponse(Dictionary<string, object?> message)
    {
        var response = GetDict(message, "response");
        if (response == null) return;

        var requestId = GetString(response, "request_id");
        if (string.IsNullOrEmpty(requestId)) return;

        if (_pendingRequests.TryRemove(requestId, out var tcs))
        {
            var subtype = GetString(response, "subtype");
            if (subtype == "error")
            {
                var error = GetString(response, "error") ?? "Unknown error";
                tcs.TrySetException(new ControlRequestException(error, requestId));
            }
            else
            {
                var responseData = GetDict(response, "response") ?? [];
                tcs.TrySetResult(responseData);
            }
        }
    }

    private async Task<Dictionary<string, object?>> SendControlRequestAsync(
        ControlRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<Dictionary<string, object?>>();
        _pendingRequests[requestId] = tcs;

        try
        {
            var controlRequest = new SdkControlRequest
            {
                RequestId = requestId,
                Request = request
            };

            var json = JsonSerializer.Serialize(controlRequest, SnakeCaseJsonOptions);

            await _transport.WriteAsync(json, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new ControlRequestTimeoutException(requestId, request.Subtype);
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task SendControlResponseAsync(
        string requestId,
        Dictionary<string, object?>? response,
        string? error,
        CancellationToken cancellationToken)
    {
        ControlResponse controlResponse = error != null
            ? new ControlErrorResponse { RequestId = requestId, Error = error }
            : new ControlSuccessResponse { RequestId = requestId, Response = response };

        var wrapper = new SdkControlResponse { Response = controlResponse };

        var json = JsonSerializer.Serialize(wrapper, SnakeCaseJsonOptions);

        await _transport.WriteAsync(json, cancellationToken);
    }

    private string GenerateRequestId()
    {
        var counter = Interlocked.Increment(ref _requestCounter);
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"req_{counter}_{random}";
    }

    private static string? GetString(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            if (value is string s) return s;
            if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element)
                return element.GetString();
        }
        return null;
    }

    private static Dictionary<string, object?>? GetDict(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            if (value is Dictionary<string, object?> d) return d;
            if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } element)
            {
                return element.Deserialize<Dictionary<string, object?>>();
            }
        }
        return null;
    }

    private static bool? GetBool(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            if (value is bool b) return b;
            if (value is System.Text.Json.JsonElement element)
            {
                if (element.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                if (element.ValueKind == System.Text.Json.JsonValueKind.False) return false;
            }
        }
        return null;
    }

    private static int? GetInt(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } element)
                return element.GetInt32();
        }
        return null;
    }

    private static List<string>? GetStringList(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            if (value is List<string> list) return list;
            if (value is IEnumerable<object?> enumerable)
                return enumerable.OfType<string>().ToList();
            if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } element)
            {
                return element.EnumerateArray()
                    .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
            }
        }
        return null;
    }
}
