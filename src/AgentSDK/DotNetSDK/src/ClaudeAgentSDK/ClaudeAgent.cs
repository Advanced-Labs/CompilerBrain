using System.Runtime.CompilerServices;
using ClaudeAgentSDK.Internal;
using ClaudeAgentSDK.Internal.Transport;
using ClaudeAgentSDK.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSDK;

/// <summary>
/// Static entry point for one-shot queries to Claude Code CLI.
/// </summary>
public static class ClaudeAgent
{
    /// <summary>
    /// Sends a one-shot query to Claude and returns the results as an async stream.
    /// This is the simplest way to interact with Claude - fire and forget.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="transport">Optional custom transport.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages from Claude.</returns>
    /// <example>
    /// <code>
    /// await foreach (var message in ClaudeAgent.QueryAsync("What is the meaning of life?"))
    /// {
    ///     if (message is AssistantMessage assistant)
    ///     {
    ///         foreach (var block in assistant.Content)
    ///         {
    ///             if (block is TextBlock text)
    ///             {
    ///                 Console.Write(text.Text);
    ///             }
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<IMessage> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        ITransport? transport = null,
        ILogger? logger = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create transport for non-streaming mode
        transport ??= new SubprocessCliTransport(
            prompt: prompt,
            options: options,
            isStreamingMode: false,
            logger: logger);

        await using (transport)
        {
            // Connect
            await transport.ConnectAsync(cancellationToken);

            // Read messages
            await foreach (var rawMessage in transport.ReadMessagesAsync(cancellationToken))
            {
                IMessage? message;
                try
                {
                    message = MessageParser.ParseMessage(rawMessage);
                }
                catch (MessageParseException ex)
                {
                    logger?.LogWarning(ex, "Failed to parse message");
                    continue;
                }

                yield return message;
            }
        }
    }

    /// <summary>
    /// Sends a query and collects all messages into a list.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all messages from the conversation.</returns>
    public static async Task<List<IMessage>> QueryToListAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<IMessage>();
        await foreach (var message in QueryAsync(prompt, options, cancellationToken: cancellationToken))
        {
            messages.Add(message);
        }
        return messages;
    }

    /// <summary>
    /// Sends a query and returns the final result text.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result text, or null if no result.</returns>
    public static async Task<string?> QueryTextAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await foreach (var message in QueryAsync(prompt, options, cancellationToken: cancellationToken))
        {
            if (message is ResultMessage result)
            {
                return result.Result;
            }
        }
        return null;
    }

    /// <summary>
    /// Sends a query with streaming input (advanced use case).
    /// </summary>
    /// <param name="input">Async enumerable of input messages.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="transport">Optional custom transport.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages from Claude.</returns>
    public static async IAsyncEnumerable<IMessage> QueryStreamAsync(
        IAsyncEnumerable<Dictionary<string, object?>> input,
        ClaudeAgentOptions? options = null,
        ITransport? transport = null,
        ILogger? logger = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create transport for streaming mode
        transport ??= new SubprocessCliTransport(
            prompt: null,
            options: options,
            isStreamingMode: true,
            logger: logger);

        await using (transport)
        {
            await transport.ConnectAsync(cancellationToken);

            // Create query handler
            var queryHandler = new QueryHandler(
                transport,
                isStreamingMode: true,
                canUseTool: options?.CanUseTool,
                hooks: options?.Hooks,
                logger: logger);

            await using (queryHandler)
            {
                queryHandler.Start();
                await queryHandler.InitializeAsync(cancellationToken);

                // Start streaming input in background
                var inputTask = queryHandler.StreamInputAsync(input, cancellationToken);

                // Read messages
                await foreach (var rawMessage in queryHandler.ReceiveMessagesAsync(cancellationToken))
                {
                    IMessage? message;
                    try
                    {
                        message = MessageParser.ParseMessage(rawMessage);
                    }
                    catch (MessageParseException ex)
                    {
                        logger?.LogWarning(ex, "Failed to parse message");
                        continue;
                    }

                    yield return message;
                }

                // Wait for input to finish
                await inputTask;
            }
        }
    }

    /// <summary>
    /// Creates a new interactive client for bidirectional communication.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="transport">Optional custom transport.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>A new ClaudeSDKClient instance.</returns>
    /// <example>
    /// <code>
    /// await using var client = ClaudeAgent.CreateClient();
    /// await client.ConnectAsync("Hello Claude!");
    ///
    /// await foreach (var message in client.ReceiveResponseAsync())
    /// {
    ///     Console.WriteLine(message);
    /// }
    ///
    /// // Send follow-up
    /// await client.QueryAsync("Tell me more");
    /// await foreach (var message in client.ReceiveResponseAsync())
    /// {
    ///     Console.WriteLine(message);
    /// }
    /// </code>
    /// </example>
    public static ClaudeSDKClient CreateClient(
        ClaudeAgentOptions? options = null,
        ITransport? transport = null,
        ILogger? logger = null)
    {
        return new ClaudeSDKClient(options, transport, logger);
    }
}
