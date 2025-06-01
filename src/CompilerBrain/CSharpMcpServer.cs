using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
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

    [McpServerTool, Description("Get filepath and code without method-body to analyze csprojct.")]
    public static CodeStructure[] GetCodeStructures(SessionMemory memory, Guid sessionId)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.GetCompilation();

        return compilation.SyntaxTrees
            .Select(tree => new CodeStructure
            {
                FilePath = tree.FilePath,
                CodeWithoutBody = CodeCompression.RemoveBody(tree)
            })
            .ToArray();
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
    public static CodeDiagnostic[] AddOrReplaceCode(SessionMemory memory, Guid sessionId, string filePath, string code)
    {
        var session = memory.GetSession(sessionId);
        var compilation = session.GetCompilation();

        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath); // TODO: parse options

        var existingTree = compilation.SyntaxTrees.FirstOrDefault(x => x.FilePath == filePath);
        var newCompilation = (existingTree == null)
            ? compilation.AddSyntaxTrees(syntaxTree)
            : compilation.ReplaceSyntaxTree(existingTree, syntaxTree);
        session.SetCompilation(newCompilation);

        return CodeDiagnostic.Errors(newCompilation.GetDiagnostics());
    }
}
