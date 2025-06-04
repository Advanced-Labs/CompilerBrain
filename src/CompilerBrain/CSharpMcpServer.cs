using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace CompilerBrain;

[McpServerToolType]
public static class CSharpMcpServer
{
    static Encoding Utf8Encoding = new UTF8Encoding(false);

    [McpServerTool, Description("Initialize the session context, require to call first before call other tools.")]
    public static Guid Initialize(SessionMemory memory)
    {
        return memory.CreateNewSession();
    }

    // TODO: Open solution(and unit-test proj!)

    // TODO: describe csproj info.

    [McpServerTool, Description("Open csprojct of the session context, returns diagnostics of compile result.")]
    public static async Task<CodeDiagnostic[]> OpenCsharpProject(SessionMemory memory, Guid sessionId, string projectPath)
    {
        using var workspace = MSBuildWorkspace.Create();

        var project = await workspace.OpenProjectAsync(projectPath);

        // case for targetframework's'? debug/release configuration? use latest target?
        var compilation = await project.GetCompilationAsync();

        if (compilation == null)
        {
            throw new InvalidOperationException("Can't get compilation.");
        }

        var session = memory.GetSession(sessionId);
        session.Compilation = compilation;
        session.ParseOptions = project.ParseOptions as CSharpParseOptions ?? CSharpParseOptions.Default;

        return CodeDiagnostic.Errors(compilation.GetDiagnostics());
    }

    [McpServerTool, Description("Get filepath and code without method-body to analyze csprojct. Data is paging so need to read mulitiple times. start page is one.")]
    public static CodeStructure GetCodeStructure(SessionMemory memory, Guid sessionId, int page)
    {
        const int FilesPerPage = 30;

        var session = memory.GetSession(sessionId);
        var compilation = session.Compilation;

        var trees = compilation.SyntaxTrees
            .Where(x => File.Exists(x.FilePath))
            .ToArray();

        var totalPage = trees.Length / FilesPerPage + 1;

        var codes = trees.Skip((page - 1) * FilesPerPage)
            .Take(FilesPerPage)
            .Select(x => new AnalyzedCode
            {
                FilePath = x.FilePath,
                CodeWithoutBody = CodeCompression.RemoveBody(x)
            })
            .ToArray();

        return new CodeStructure
        {
            Page = page,
            TotalPage = totalPage,
            Codes = codes
        };
    }

    [McpServerTool, Description("Read existing code in current session context, if not found returns null.")]
    public static string? ReadCode(SessionMemory memory, Guid sessionId, string filePath, string code)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.Compilation;

        if (!compilation.SyntaxTrees.TryGet(filePath, out var existingTree) || !existingTree.TryGetText(out var text))
        {
            return null;
        }

        return text.ToString();
    }

    [McpServerTool, Description("Add or replace new code to current session context, returns diagnostics of compile result.")]
    public static AddOrReplaceResult AddOrReplaceCode(SessionMemory memory, Guid sessionId, Codes[] codes)
    {
        try
        {
            var session = memory.GetSession(sessionId);
            var compilation = session.Compilation;
            var parseOptions = session.ParseOptions;

            if (codes.Length == 0)
            {
                return new AddOrReplaceResult { CodeChanges = [], Diagnostics = [] };
            }

            Compilation newCompilation = default!;
            List<CodeChange> codeChanges = new();
            foreach (var item in codes)
            {
                var code = item.Code;
                var filePath = item.FilePath;

                var oldTree = compilation.SyntaxTrees.FirstOrDefault(x => x.FilePath == filePath);

                if (oldTree != null)
                {
                    // apply same line-break
                    var lineBreak = oldTree.GetLineBreakFromFirstLine();
                    code = code.ReplaceLineEndings(lineBreak);

                    var newTree = oldTree.WithChangedText(SourceText.From(code));
                    var changes = newTree.GetChanges(oldTree);

                    var lineChanges = new LineChanges[changes.Count];
                    var i = 0;
                    foreach (var change in changes)
                    {
                        var changeText = GetLineText(oldTree, change.Span);
                        lineChanges[i++] = new LineChanges { RemoveLine = changeText.ToString(), AddLine = change.NewText };
                    }

                    codeChanges.Add(new CodeChange { FilePath = filePath, LineChanges = lineChanges });
                    newCompilation = compilation.ReplaceSyntaxTree(oldTree, newTree);
                    session.RemoveNewCode(oldTree);
                    session.AddNewCode(newTree);
                }
                else
                {
                    var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, path: filePath);
                    codeChanges.Add(new CodeChange { FilePath = filePath, LineChanges = [new LineChanges { RemoveLine = null, AddLine = code }] });
                    newCompilation = compilation.AddSyntaxTrees(syntaxTree);
                    session.AddNewCode(syntaxTree);
                }
            }

            session.Compilation = newCompilation;
            var diagnostics = CodeDiagnostic.Errors(newCompilation.GetDiagnostics());

            var result = new AddOrReplaceResult
            {
                CodeChanges = codeChanges.ToArray(),
                Diagnostics = diagnostics
            };

            return result;
        }
        catch (Exception ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }

    [McpServerTool, Description("Save add-or-replaced codes in current in-memory session context, return value is saved paths.")]
    public static string[] SaveCodeToDisc(SessionMemory memory, Guid sessionId)
    {
        var session = memory.GetSession(sessionId);
        var newSources = session.ClearNewCodes();
        var result = new string[newSources.Length];
        var i = 0;
        foreach (var item in newSources)
        {
            File.WriteAllText(item.FilePath, item.GetText().ToString(), Utf8Encoding);
            result[i++] = item.FilePath;
        }
        return result;
    }

    static void RunUnitTest(SessionMemory memory, Guid sessionId)
    {
        // TODO: Emit inline image and reference.
        var session = memory.GetSession(sessionId);

        using var libraryStream = new MemoryStream();
        var r = session.Compilation.Emit(libraryStream);
    }

    [McpServerTool, Description("Search for code patterns using regular expressions in files matching the target file pattern.")]
    public static SearchResult SearchCodeByRegex(SessionMemory memory, Guid sessionId, string targetFileRegex, string searchRegex)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.Compilation;

        // accept user generated regex patterns so no-compiled and non-backtracking options are used.
        var targetFilePattern = new Regex(targetFileRegex, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
        var searchPattern = new Regex(searchRegex, RegexOptions.Multiline | RegexOptions.NonBacktracking);

        var matches = new List<SearchMatch>();

        // Filter syntax trees by file path pattern
        var targetTrees = compilation.SyntaxTrees
            .Where(tree => !string.IsNullOrEmpty(tree.FilePath) && 
                          File.Exists(tree.FilePath) && 
                          targetFilePattern.IsMatch(Path.GetFileName(tree.FilePath)))
            .ToArray();

        foreach (var syntaxTree in targetTrees)
        {
            if (syntaxTree.TryGetText(out var sourceText))
            {
                var fullText = sourceText.ToString();
                var regexMatches = searchPattern.Matches(fullText);
                var root = syntaxTree.GetRoot();

                foreach (Match match in regexMatches)
                {
                    var textSpan = new TextSpan(match.Index, match.Length);
                    var linePosition = sourceText.Lines.GetLinePosition(match.Index);
                    var lineText = sourceText.Lines[linePosition.Line].ToString();

                    // Find the syntax node that contains this match
                    var context = AnalyzeSyntaxContext(root, match.Index);

                    matches.Add(new SearchMatch
                    {
                        FilePath = syntaxTree.FilePath,
                        LineNumber = linePosition.Line + 1, // 1-based line number
                        ColumnNumber = linePosition.Character + 1, // 1-based column number
                        LineText = lineText,
                        MatchedText = match.Value,
                        Location = new CodeLocation(match.Index, match.Length),
                        Context = context
                    });
                }
            }
        }

        return new SearchResult
        {
            Matches = matches.ToArray(),
            TotalMatches = matches.Count
        };
    }

    static CodeContext AnalyzeSyntaxContext(SyntaxNode root, int position)
    {
        var node = root.FindToken(position).Parent;
        
        string? className = null;
        string? methodName = null;
        string? propertyName = null;
        string? fieldName = null;
        string? namespaceName = null;
        string syntaxKind = "Unknown";
        string containingMember = "Global";

        // Walk up the syntax tree to find containing members
        var current = node;
        while (current != null)
        {
            switch (current)
            {
                case NamespaceDeclarationSyntax ns:
                    namespaceName = ns.Name.ToString();
                    break;
                case FileScopedNamespaceDeclarationSyntax fileNs:
                    namespaceName = fileNs.Name.ToString();
                    break;
                case ClassDeclarationSyntax cls:
                    className = cls.Identifier.ValueText;
                    break;
                case RecordDeclarationSyntax record:
                    className = record.Identifier.ValueText + " (record)";
                    break;
                case StructDeclarationSyntax str:
                    className = str.Identifier.ValueText + " (struct)";
                    break;
                case InterfaceDeclarationSyntax iface:
                    className = iface.Identifier.ValueText + " (interface)";
                    break;
                case MethodDeclarationSyntax method:
                    methodName = method.Identifier.ValueText;
                    break;
                case ConstructorDeclarationSyntax ctor:
                    methodName = ".ctor";
                    break;
                case PropertyDeclarationSyntax prop:
                    propertyName = prop.Identifier.ValueText;
                    break;
                case FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0:
                    fieldName = field.Declaration.Variables[0].Identifier.ValueText;
                    break;
                case LocalFunctionStatementSyntax localFunc:
                    methodName = localFunc.Identifier.ValueText + " (local)";
                    break;
            }
            current = current.Parent;
        }

        // Determine the immediate syntax kind
        if (node != null)
        {
            syntaxKind = node.Kind().ToString();
        }

        // Build containing member description
        var memberParts = new List<string>();
        if (!string.IsNullOrEmpty(namespaceName))
            memberParts.Add(namespaceName);
        if (!string.IsNullOrEmpty(className))
            memberParts.Add(className);
        if (!string.IsNullOrEmpty(methodName))
            memberParts.Add(methodName);
        else if (!string.IsNullOrEmpty(propertyName))
            memberParts.Add(propertyName);
        else if (!string.IsNullOrEmpty(fieldName))
            memberParts.Add(fieldName);

        containingMember = memberParts.Count > 0 ? string.Join(".", memberParts) : "Global";

        return new CodeContext
        {
            ClassName = className,
            MethodName = methodName,
            PropertyName = propertyName,
            FieldName = fieldName,
            NamespaceName = namespaceName,
            SyntaxKind = syntaxKind,
            ContainingMember = containingMember
        };
    }

    // TODO: SymbolFinder

    static SourceText GetLineText(SyntaxTree syntaxTree, TextSpan textSpan)
    {
        var sourceText = syntaxTree.GetText();
        var linePositionSpan = sourceText.Lines.GetLinePositionSpan(textSpan);
        var lineSpan = sourceText.Lines.GetTextSpan(linePositionSpan);
        return sourceText.GetSubText(lineSpan);
    }
}
