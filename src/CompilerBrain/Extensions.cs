using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CompilerBrain;

internal static class Extensions
{
    internal static bool TryGet(this IEnumerable<SyntaxTree> syntaxTrees, string filePath, [MaybeNullWhen(false)] out SyntaxTree syntaxTree)
    {
        if (syntaxTrees is ImmutableArray<SyntaxTree> immutableArray)
        {
            foreach (var tree in immutableArray) // faster iteration
            {
                if (tree.FilePath == filePath)
                {
                    syntaxTree = tree;
                    return true;
                }
            }
        }
        else
        {
            foreach (var tree in syntaxTrees)
            {
                if (tree.FilePath == filePath)
                {
                    syntaxTree = tree;
                    return true;
                }
            }
        }

        syntaxTree = null;
        return false;
    }
}
