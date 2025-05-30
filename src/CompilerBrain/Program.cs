using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.ComponentModel;
using ZLinq;
using ZLogger;

// ZLinq drop-in everything
[assembly: ZLinq.ZLinqDropInAttribute("", ZLinq.DropInGenerateTypes.Everything)]

// Debugger.Launch(); // for DEBUGGING.

MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddZLoggerConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton<SessionMemory>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([typeof(CSharpMcpServer)]);

await builder.Build().RunAsync();

[McpServerToolType]
public static class CSharpMcpServer
{
    [McpServerTool, Description("Initialize the session context, require to call first before call other tools.")]
    public static Guid Initialize(SessionMemory memory)
    {
        return memory.CreateNewSession();
    }

    [McpServerTool, Description("Open csprojct of the session context, returns diagnostics of compile result.")]
    public static async Task<CodeDiagnostic[]> OpenCsharpProject(SessionMemory memory, Guid sessionId, string projectPath)
    {
        using var workspace = MSBuildWorkspace.Create();

        var project = await workspace.OpenProjectAsync(projectPath);

        // case for targetframework's'? debug/release configuration?
        var compilation = await project.GetCompilationAsync();

        if (compilation == null)
        {
            throw new InvalidOperationException("Can't get compilation.");
        }

        var session = memory.GetSession(sessionId);
        session.Compilation = compilation;

        return compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => new CodeDiagnostic(x))
            .ToArray();
    }

    [McpServerTool, Description("Read existing code in current session context, if not found returns null.")]
    public static string? ReadCode(SessionMemory memory, Guid sessionId, string filePath, string code)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.Compilation;
        if (compilation == null)
        {
            throw new InvalidOperationException();
        }

        var existingTree = compilation.SyntaxTrees.FirstOrDefault(x => x.FilePath == filePath);

        if (existingTree == null || !existingTree.TryGetText(out var text)) return null;

        return text.ToString();
    }

    // TODO: line-diff

    [McpServerTool, Description("Add or replace new code to current session context, returns diagnostics of compile result.")]
    public static CodeDiagnostic[] AddOrReplaceCode(SessionMemory memory, Guid sessionId, string filePath, string code)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.Compilation;
        if (compilation == null)
        {
            throw new InvalidOperationException();
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath); // TODO: parse options

        var existingTree = compilation.SyntaxTrees.FirstOrDefault(x => x.FilePath == filePath);
        var newCompilation = (existingTree == null)
            ? compilation.AddSyntaxTrees(syntaxTree)
            : compilation.ReplaceSyntaxTree(existingTree, syntaxTree);
        session.Compilation = newCompilation;

        return newCompilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => new CodeDiagnostic(x))
            .ToArray();
    }
}


public class SessionMemory
{
    ConcurrentDictionary<Guid, CompilerSession> sessions;

    public SessionMemory()
    {
        sessions = new ConcurrentDictionary<Guid, CompilerSession>();
    }

    public Guid CreateNewSession()
    {
        var id = Guid.NewGuid();
        var session = new CompilerSession(DateTime.UtcNow);
        sessions.TryAdd(id, session);
        return id;
    }

    public CompilerSession GetSession(Guid id)
    {
        if (!sessions.TryGetValue(id, out var session))
        {
            throw new InvalidOperationException("Session is not found. Id:" + id);
        }
        return session;
    }

}

public class CompilerSession(DateTime startTime)
{
    public DateTime StartTime { get; } = startTime;
    public Compilation? Compilation { get; set; }
}

public class CodeDiagnostic
{
    public string Code { get; }
    public string Description { get; }
    public string FilePath { get; }
    public CodeLocation Location { get; }

    public CodeDiagnostic(Diagnostic diagnostic)
    {
        Code = diagnostic.Id;
        Description = diagnostic.ToString();
        FilePath = diagnostic.Location.SourceTree?.FilePath ?? "";
        Location = new(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
    }
}

public readonly record struct CodeLocation(int Start, int Length);