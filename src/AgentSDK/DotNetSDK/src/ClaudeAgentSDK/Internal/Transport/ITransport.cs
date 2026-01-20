namespace ClaudeAgentSDK.Internal.Transport;

/// <summary>
/// Abstract transport interface for communicating with Claude Code CLI.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the transport is ready for communication.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Connects and starts the transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to the transport's input stream.
    /// </summary>
    /// <param name="data">The data to write (typically JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads messages from the transport's output stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of parsed JSON messages.</returns>
    IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals the end of input to the transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EndInputAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the transport and cleans up resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
