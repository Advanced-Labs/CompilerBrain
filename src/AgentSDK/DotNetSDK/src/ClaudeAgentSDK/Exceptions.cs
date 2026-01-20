namespace ClaudeAgentSDK;

/// <summary>
/// Base exception for all Claude SDK errors.
/// </summary>
public class ClaudeSDKException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ClaudeSDKException class.
    /// </summary>
    public ClaudeSDKException() : base() { }

    /// <summary>
    /// Initializes a new instance of the ClaudeSDKException class with a message.
    /// </summary>
    public ClaudeSDKException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the ClaudeSDKException class with a message and inner exception.
    /// </summary>
    public ClaudeSDKException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when unable to connect to Claude Code CLI.
/// </summary>
public class CliConnectionException : ClaudeSDKException
{
    /// <summary>
    /// Initializes a new instance of the CliConnectionException class.
    /// </summary>
    public CliConnectionException() : base() { }

    /// <summary>
    /// Initializes a new instance of the CliConnectionException class with a message.
    /// </summary>
    public CliConnectionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the CliConnectionException class with a message and inner exception.
    /// </summary>
    public CliConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when Claude Code CLI is not found or not installed.
/// </summary>
public class CliNotFoundException : CliConnectionException
{
    /// <summary>
    /// Initializes a new instance of the CliNotFoundException class.
    /// </summary>
    public CliNotFoundException()
        : base("Claude Code CLI not found. Please install it using: npm install -g @anthropic/claude-code") { }

    /// <summary>
    /// Initializes a new instance of the CliNotFoundException class with a message.
    /// </summary>
    public CliNotFoundException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the CliNotFoundException class with a message and inner exception.
    /// </summary>
    public CliNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Raised when the CLI process fails.
/// </summary>
public class ProcessException : ClaudeSDKException
{
    /// <summary>
    /// The exit code of the process.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// The stderr output from the process.
    /// </summary>
    public string? Stderr { get; }

    /// <summary>
    /// Initializes a new instance of the ProcessException class.
    /// </summary>
    public ProcessException(string message, int? exitCode = null, string? stderr = null)
        : base(message)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    /// <summary>
    /// Initializes a new instance of the ProcessException class with an inner exception.
    /// </summary>
    public ProcessException(string message, Exception innerException, int? exitCode = null, string? stderr = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }
}

/// <summary>
/// Raised when unable to decode JSON from CLI output.
/// </summary>
public class CliJsonDecodeException : ClaudeSDKException
{
    /// <summary>
    /// The line that failed to parse.
    /// </summary>
    public string Line { get; }

    /// <summary>
    /// Initializes a new instance of the CliJsonDecodeException class.
    /// </summary>
    public CliJsonDecodeException(string message, string line)
        : base(message)
    {
        Line = line;
    }

    /// <summary>
    /// Initializes a new instance of the CliJsonDecodeException class with an inner exception.
    /// </summary>
    public CliJsonDecodeException(string message, string line, Exception innerException)
        : base(message, innerException)
    {
        Line = line;
    }
}

/// <summary>
/// Raised when unable to parse a message from CLI output.
/// </summary>
public class MessageParseException : ClaudeSDKException
{
    /// <summary>
    /// The raw data that failed to parse.
    /// </summary>
    public Dictionary<string, object?>? RawData { get; }

    /// <summary>
    /// Initializes a new instance of the MessageParseException class.
    /// </summary>
    public MessageParseException(string message, Dictionary<string, object?>? data = null)
        : base(message)
    {
        RawData = data;
    }

    /// <summary>
    /// Initializes a new instance of the MessageParseException class with an inner exception.
    /// </summary>
    public MessageParseException(string message, Dictionary<string, object?>? data, Exception innerException)
        : base(message, innerException)
    {
        RawData = data;
    }
}

/// <summary>
/// Raised when a control request times out.
/// </summary>
public class ControlRequestTimeoutException : ClaudeSDKException
{
    /// <summary>
    /// The request ID that timed out.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// The type of request that timed out.
    /// </summary>
    public string RequestType { get; }

    /// <summary>
    /// Initializes a new instance of the ControlRequestTimeoutException class.
    /// </summary>
    public ControlRequestTimeoutException(string requestId, string requestType)
        : base($"Control request '{requestType}' (ID: {requestId}) timed out")
    {
        RequestId = requestId;
        RequestType = requestType;
    }
}

/// <summary>
/// Raised when a control request fails.
/// </summary>
public class ControlRequestException : ClaudeSDKException
{
    /// <summary>
    /// The request ID that failed.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// Initializes a new instance of the ControlRequestException class.
    /// </summary>
    public ControlRequestException(string message, string requestId)
        : base(message)
    {
        RequestId = requestId;
    }

    /// <summary>
    /// Initializes a new instance of the ControlRequestException class with an inner exception.
    /// </summary>
    public ControlRequestException(string message, string requestId, Exception innerException)
        : base(message, innerException)
    {
        RequestId = requestId;
    }
}

/// <summary>
/// Raised when the client is not connected.
/// </summary>
public class NotConnectedException : ClaudeSDKException
{
    /// <summary>
    /// Initializes a new instance of the NotConnectedException class.
    /// </summary>
    public NotConnectedException()
        : base("Client is not connected. Call Connect() first.") { }

    /// <summary>
    /// Initializes a new instance of the NotConnectedException class with a message.
    /// </summary>
    public NotConnectedException(string message) : base(message) { }
}

/// <summary>
/// Raised when an invalid operation is attempted.
/// </summary>
public class InvalidOperationException : ClaudeSDKException
{
    /// <summary>
    /// Initializes a new instance of the InvalidOperationException class.
    /// </summary>
    public InvalidOperationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the InvalidOperationException class with an inner exception.
    /// </summary>
    public InvalidOperationException(string message, Exception innerException) : base(message, innerException) { }
}
