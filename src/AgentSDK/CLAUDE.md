# Workflow

## Python SDK

```bash
cd PythonSDK

# Lint and style
# Check for issues and fix automatically
python -m ruff check src/ tests/ --fix
python -m ruff format src/ tests/

# Typecheck (only done for src/)
python -m mypy src/

# Run all tests
python -m pytest tests/

# Run specific test file
python -m pytest tests/test_client.py
```

## .NET SDK

```bash
cd DotNetSDK

# Build
dotnet build

# Run tests
dotnet test

# Pack NuGet package
dotnet pack
```

# Codebase Structure

## Python SDK (`PythonSDK/`)

- `src/claude_agent_sdk/` - Main package
  - `client.py` - ClaudeSDKClient for interactive sessions
  - `query.py` - One-shot query function
  - `types.py` - Type definitions
  - `_internal/` - Internal implementation details
    - `transport/subprocess_cli.py` - CLI subprocess management
    - `message_parser.py` - Message parsing logic

## .NET SDK (`DotNetSDK/`)

- `src/ClaudeAgentSDK/` - Main package
  - `ClaudeAgent.cs` - Static entry point for one-shot queries
  - `ClaudeSDKClient.cs` - Interactive client for bidirectional communication
  - `Models/` - Type definitions (Messages, Options, Hooks, Permissions)
  - `Internal/` - Internal implementation details
    - `Transport/SubprocessCliTransport.cs` - CLI subprocess management
    - `MessageParser.cs` - Message parsing logic
    - `QueryHandler.cs` - Control protocol handler
