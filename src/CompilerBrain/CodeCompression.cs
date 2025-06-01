using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CompilerBrain;

public static class CodeCompression
{
    public static string RemoveBody(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetRoot();
        var newNode = new BodyRemovalRewriter().Visit(root);
        return newNode.ToFullString();
    }

    sealed class BodyRemovalRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body != null)
            {
                return node.WithBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Body != null)
            {
                return node.WithBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            return base.VisitConstructorDeclaration(node);
        }

        public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.Body != null)
            {
                return node.WithBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            return base.VisitAccessorDeclaration(node);
        }
    }
}
