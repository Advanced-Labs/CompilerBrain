using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace CompilerBrain;

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
    CSharpParseOptions? parseOptions;
    Compilation? compilation;

    public DateTime StartTime { get; } = startTime;
    HashSet<SyntaxTree> newCodes = new();

    public CSharpParseOptions ParseOptions
    {
        get
        {
            if (parseOptions == null)
            {
                throw new InvalidOperationException("ParseOptions is not set.");
            }
            return parseOptions;
        }
        set
        {
            parseOptions = value;
        }
    }

    public Compilation Compilation
    {
        get
        {
            if (compilation == null)
            {
                throw new InvalidOperationException("Compilation is not set.");
            }
            return compilation;
        }
        set
        {
            compilation = value;
        }
    }

    public void AddNewCode(SyntaxTree syntaxTree)
    {
        newCodes.Add(syntaxTree);
    }

    public void RemoveNewCode(SyntaxTree syntaxTree)
    {
        newCodes.Remove(syntaxTree);
    }

    public SyntaxTree[] ClearNewCodes()
    {
        var result = newCodes.ToArray();
        newCodes.Clear();
        return result;
    }
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

    public static CodeDiagnostic[] Errors(ImmutableArray<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => new CodeDiagnostic(x))
            .ToArray();
    }
}

public readonly record struct CodeLocation(int Start, int Length);

public readonly record struct CodeStructure
{
    public required int Page { get; init; }
    public required int TotalPage { get; init; }
    public required AnalyzedCode[] Codes { get; init; }
}

public readonly record struct AnalyzedCode
{
    public required string FilePath { get; init; }
    public required string CodeWithoutBody { get; init; }
}

public readonly record struct Codes
{
    public required string FilePath { get; init; }
    public required string Code { get; init; }
}

public readonly record struct AddOrReplaceResult
{
    public required CodeChange[] CodeChanges { get; init; }
    public required CodeDiagnostic[] Diagnostics { get; init; }
}

public readonly record struct CodeChange
{
    public required string FilePath { get; init; }
    public required LineChanges[] LineChanges { get; init; }
}

public readonly record struct LineChanges
{
    public required string? RemoveLine { get; init; }
    public required string? AddLine { get; init; }

    public override string ToString()
    {
        return (RemoveLine, AddLine) switch
        {
            (null, null) => "",
            (var remove, null) => "-" + remove,
            (null, var add) => "+" + add,
            (var remove, var add) => "-" + remove + Environment.NewLine + "+" + add,
        };
    }
}

// Search result structures
public readonly record struct SearchResult
{
    public required SearchMatch[] Matches { get; init; }
    public required int TotalMatches { get; init; }
}

public readonly record struct SearchMatch
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required int ColumnNumber { get; init; }
    public required string LineText { get; init; }
    public required string MatchedText { get; init; }
    public required CodeLocation Location { get; init; }
}