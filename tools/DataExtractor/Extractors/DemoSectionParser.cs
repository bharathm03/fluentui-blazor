using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataExtractor.Extractors;

public record DemoSectionInfo(string TypeName, string Title, string Description, List<string> AdditionalFiles);

public static partial class DemoSectionParser
{
    private static readonly RazorProjectEngine RazorEngine = RazorProjectEngine.Create(
        RazorConfiguration.Default,
        RazorProjectFileSystem.Create("/"),
        b => b.SetRootNamespace("Generated"));

    /// <summary>
    /// Parses all &lt;DemoSection&gt; blocks from a Razor page using
    /// the Razor compiler → Roslyn syntax tree pipeline.
    /// </summary>
    public static List<DemoSectionInfo> Parse(string pageContent)
    {
        var csharp = CompileRazorToCSharp(pageContent);
        var tree = CSharpSyntaxTree.ParseText(csharp);
        var root = tree.GetRoot();
        return ExtractDemoSections(root);
    }

    private static string CompileRazorToCSharp(string razorContent)
    {
        var item = new VirtualProjectItem("/", "/Page.razor", razorContent);
        var document = RazorEngine.Process(item);
        return document.GetCSharpDocument().Text.ToString();
    }

    private static List<DemoSectionInfo> ExtractDemoSections(SyntaxNode root)
    {
        var results = new List<DemoSectionInfo>();
        var elementStack = new Stack<string>();
        string? currentTypeName = null;
        string? currentTitle = null;
        List<string>? currentAdditionalFiles = null;
        StringBuilder? descriptionBuilder = null;
        bool inDemoSection = false;
        bool inDescription = false;

        // Walk all invocations in document order
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var (receiver, methodName) = GetMethodInfo(invocation);
            if (receiver != "__builder" || methodName == null)
            {
                continue;
            }

            var args = invocation.ArgumentList.Arguments;

            switch (methodName)
            {
                case "OpenElement":
                    if (args.Count >= 2)
                    {
                        var elementName = GetStringLiteral(args[1].Expression);
                        if (elementName != null)
                        {
                            elementStack.Push(elementName);

                            if (elementName == "DemoSection")
                            {
                                inDemoSection = true;
                                currentTypeName = null;
                                currentTitle = null;
                                currentAdditionalFiles = null;
                                descriptionBuilder = null;
                                inDescription = false;
                            }
                            else if (elementName == "Description" && inDemoSection)
                            {
                                inDescription = true;
                                descriptionBuilder = new StringBuilder();
                            }
                        }
                    }
                    break;

                case "AddAttribute":
                    if (inDemoSection && !inDescription && args.Count >= 3)
                    {
                        var attrName = GetStringLiteral(args[1].Expression);
                        switch (attrName)
                        {
                            case "Title":
                                currentTitle = GetStringLiteral(args[2].Expression);
                                break;
                            case "Component":
                                currentTypeName = ExtractTypeName(args[2].Expression);
                                break;
                            case "AdditionalFiles":
                                currentAdditionalFiles = ExtractStringList(args[2].Expression);
                                break;
                        }
                    }
                    break;

                case "AddMarkupContent":
                case "AddContent":
                    if (inDescription && descriptionBuilder != null && args.Count >= 2)
                    {
                        var content = GetStringLiteral(args[1].Expression);
                        if (content != null)
                        {
                            descriptionBuilder.Append(content);
                        }
                    }
                    break;

                case "CloseElement":
                    if (elementStack.Count > 0)
                    {
                        var closed = elementStack.Pop();

                        if (closed == "Description" && inDemoSection)
                        {
                            inDescription = false;
                        }
                        else if (closed == "DemoSection")
                        {
                            if (!string.IsNullOrEmpty(currentTypeName))
                            {
                                var description = descriptionBuilder != null
                                    ? HtmlToPlainText.Convert(descriptionBuilder.ToString())
                                    : "";

                                results.Add(new DemoSectionInfo(
                                    currentTypeName,
                                    currentTitle ?? "",
                                    description,
                                    currentAdditionalFiles ?? new List<string>()));
                            }

                            inDemoSection = false;
                            inDescription = false;
                            currentTypeName = null;
                            currentTitle = null;
                            currentAdditionalFiles = null;
                            descriptionBuilder = null;
                        }
                    }
                    break;
            }
        }

        return results;
    }

    private static (string? Receiver, string? MethodName) GetMethodInfo(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            return (identifier.Identifier.Text, memberAccess.Name.Identifier.Text);
        }
        return (null, null);
    }

    private static string? GetStringLiteral(ExpressionSyntax expr)
    {
        expr = Unwrap(expr);

        if (expr is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private static string? ExtractTypeName(ExpressionSyntax expr)
    {
        expr = Unwrap(expr);

        // Direct typeof(Xxx) expression
        if (expr is TypeOfExpressionSyntax typeOf)
        {
            return typeOf.Type.ToString();
        }

        // String literal containing "typeof(Xxx)" — fallback for non-expression attributes
        if (expr is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var match = TypeofInStringRegex().Match(literal.Token.ValueText);
            return match.Success ? match.Groups[1].Value : null;
        }

        // Wrapped in an invocation like RuntimeHelpers.TypeCheck<T>(typeof(Xxx))
        if (expr is InvocationExpressionSyntax invocation)
        {
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                var result = ExtractTypeName(arg.Expression);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static List<string> ExtractStringList(ExpressionSyntax expr)
    {
        expr = Unwrap(expr);

        InitializerExpressionSyntax? initializer = null;

        if (expr is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            initializer = implicitArray.Initializer;
        }
        else if (expr is ArrayCreationExpressionSyntax array)
        {
            initializer = array.Initializer;
        }
        else if (expr is CollectionExpressionSyntax collection)
        {
            return collection.Elements
                .OfType<ExpressionElementSyntax>()
                .Select(e => GetStringLiteral(e.Expression))
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();
        }

        if (initializer == null)
        {
            return new List<string>();
        }

        return initializer.Expressions
            .Select(e => GetStringLiteral(e))
            .Where(s => s != null)
            .Select(s => s!)
            .ToList();
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expr)
    {
        while (true)
        {
            if (expr is CastExpressionSyntax cast)
            {
                expr = cast.Expression;
            }
            else if (expr is ParenthesizedExpressionSyntax paren)
            {
                expr = paren.Expression;
            }
            else
            {
                return expr;
            }
        }
    }

    [GeneratedRegex(@"typeof\(([^)]+)\)")]
    private static partial Regex TypeofInStringRegex();
}
