using System.Runtime.CompilerServices;
using ClaudeAgentSDK.Internal;
using ClaudeAgentSDK.Internal.Transport;
using ClaudeAgentSDK.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSDK;

/// <summary>
/// Interactive client for bidirectional communication with Claude Code CLI.
/// Supports streaming mode with interrupts, model changes, and file rewind.
/// </summary>
public sealed class ClaudeSDKClient : IAsyncDisposable
{
    private readonly ClaudeAgentOptions? _options;
    private readonly ITransport? _customTransport;
    private readonly ILogger? _logger;

    private ITransport? _transport;
    private QueryHandler? _queryHandler;
    private bool _isConnected;
    private bool _isDisposed;

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public bool IsConnected => _isConnected && _transport?.IsReady == true;

    /// <summary>
    /// Gets the server initialization info (available after Connect in streaming mode).
    /// </summary>
    public Dictionary<string, object?>? ServerInfo => _queryHandler?.ServerInfo;

    /// <summary>
    /// Creates a new ClaudeSDKClient instance.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="transport">Optional custom transport (defaults to SubprocessCliTransport).</param>
    /// <param name="logger">Optional logger.</param>
    public ClaudeSDKClient(
        ClaudeAgentOptions? options = null,
        ITransport? transport = null,
        ILogger? logger = null)
    {
        _options = options;
        _customTransport = transport;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the CLI and optionally starts streaming with an initial prompt.
    /// </summary>
    /// <param name="prompt">Optional initial prompt to start the conversation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(string? prompt = null, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (_isConnected)
            throw new InvalidOperationException("Client is already connected");

        // Create transport
        _transport = _customTransport ?? new SubprocessCliTransport(
            prompt: null, // We'll send prompt after initialization in streaming mode
            options: _options,
            isStreamingMode: true,
            logger: _logger);

        // Create query handler
        _queryHandler = new QueryHandler(
            _transport,
            isStreamingMode: true,
            canUseTool: _options?.CanUseTool,
            hooks: _options?.Hooks,
            logger: _logger);

        // Connect transport
        await _transport.ConnectAsync(cancellationToken);

        // Start query handler
        _queryHandler.Start();

        // Initialize control protocol
        await _queryHandler.InitializeAsync(cancellationToken);

        _isConnected = true;
        _logger?.LogInformation("Connected to Claude Code CLI");

        // Send initial prompt if provided
        if (!string.IsNullOrEmpty(prompt))
        {
            await _queryHandler.SendQueryAsync(prompt, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Receives all messages from the CLI as an async stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages.</returns>
    public async IAsyncEnumerable<IMessage> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        await foreach (var rawMessage in _queryHandler!.ReceiveMessagesAsync(cancellationToken))
        {
            IMessage? message = null;
            try
            {
                message = MessageParser.ParseMessage(rawMessage);
            }
            catch (MessageParseException ex)
            {
                _logger?.LogWarning(ex, "Failed to parse message");
                // Skip unparseable messages
                continue;
            }

            yield return message;
        }
    }

    /// <summary>
    /// Receives messages until a ResultMessage is received.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages.</returns>
    public async IAsyncEnumerable<IMessage> ReceiveResponseAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveMessagesAsync(cancellationToken))
        {
            yield return message;

            if (message is ResultMessage)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Sends a new query in the current session.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="sessionId">Optional session ID for continuing a specific session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task QueryAsync(string prompt, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _queryHandler!.SendQueryAsync(prompt, sessionId, cancellationToken);
    }

    /// <summary>
    /// Streams input messages to the CLI.
    /// </summary>
    /// <param name="input">Async enumerable of input messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamInputAsync(
        IAsyncEnumerable<Dictionary<string, object?>> input,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _queryHandler!.StreamInputAsync(input, cancellationToken);
    }

    /// <summary>
    /// Sends an interrupt signal to stop the current operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _queryHandler!.InterruptAsync(cancellationToken);
    }

    /// <summary>
    /// Changes the permission mode during the conversation.
    /// </summary>
    /// <param name="mode">The new permission mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var modeStr = mode switch
        {
            PermissionMode.Default => "default",
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            _ => "default"
        };
        await _queryHandler!.SetPermissionModeAsync(modeStr, cancellationToken);
    }

    /// <summary>
    /// Changes the AI model during the conversation.
    /// </summary>
    /// <param name="model">The model name (or null to use default).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetModelAsync(string? model, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _queryHandler!.SetModelAsync(model, cancellationToken);
    }

    /// <summary>
    /// Rewinds files to a specific checkpoint (requires file checkpointing enabled).
    /// </summary>
    /// <param name="userMessageId">The user message ID to rewind to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _queryHandler!.RewindFilesAsync(userMessageId, cancellationToken);
    }

    /// <summary>
    /// Disconnects from the CLI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected) return;

        _isConnected = false;

        if (_queryHandler != null)
        {
            await _queryHandler.CloseAsync();
        }

        if (_transport != null)
        {
            await _transport.EndInputAsync(cancellationToken);
            await _transport.CloseAsync(cancellationToken);
        }

        _logger?.LogInformation("Disconnected from Claude Code CLI");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await DisconnectAsync();

        if (_queryHandler != null)
        {
            await _queryHandler.DisposeAsync();
        }

        if (_transport != null)
        {
            await _transport.DisposeAsync();
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void EnsureConnected()
    {
        EnsureNotDisposed();
        if (!_isConnected || _queryHandler == null)
            throw new NotConnectedException();
    }
}
