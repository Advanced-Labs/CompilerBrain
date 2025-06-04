using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CompilerBrain;

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
        session.SetCompilation(compilation);

        return CodeDiagnostic.Errors(compilation.GetDiagnostics());
    }

    [McpServerTool, Description("Get filepath and code without method-body to analyze csprojct. Data is paging so need to read mulitiple times. start page is one.")]
    public static CodeStructure GetCodeStructure(SessionMemory memory, Guid sessionId, int page)
    {
        const int FilesPerPage = 30;

        var session = memory.GetSession(sessionId);
        var compilation = session.GetCompilation();

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
        var compilation = session.GetCompilation();

        if (!compilation.SyntaxTrees.TryGet(filePath, out var existingTree) || !existingTree.TryGetText(out var text))
        {
            return null;
        }

        return text.ToString();
    }

    // TODO: line-diff

    [McpServerTool, Description("Add or replace new code to current session context, returns diagnostics of compile result.")]
    public static AddOrReplaceResult AddOrReplaceCode(SessionMemory memory, Guid sessionId, Codes[] codes)
    {
        try
        {
            var session = memory.GetSession(sessionId);
            var compilation = session.GetCompilation();

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
                }
                else
                {
                    var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath); // TODO: parse options
                    codeChanges.Add(new CodeChange { FilePath = filePath, LineChanges = [new LineChanges { RemoveLine = null, AddLine = code }] });
                    newCompilation = compilation.AddSyntaxTrees(syntaxTree);
                }
            }

            session.SetCompilation(newCompilation);
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

    static SourceText GetLineText(SyntaxTree syntaxTree, TextSpan textSpan)
    {
        var sourceText = syntaxTree.GetText();
        var linePositionSpan = sourceText.Lines.GetLinePositionSpan(textSpan);
        var lineSpan = sourceText.Lines.GetTextSpan(linePositionSpan);
        return sourceText.GetSubText(lineSpan);
    }
}
