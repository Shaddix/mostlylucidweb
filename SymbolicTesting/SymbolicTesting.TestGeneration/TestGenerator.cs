using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SymbolicTesting.Core;
using System.Reflection;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SymbolicTesting.TestGeneration;

/// <summary>
/// Generates xUnit test code from symbolic execution results
/// </summary>
public class TestGenerator
{
    /// <summary>
    /// Generates a test class containing all test cases
    /// </summary>
    public string GenerateTestClass(
        Type targetType,
        MethodInfo targetMethod,
        ExplorationResult explorationResult)
    {
        var className = $"{targetType.Name}_{targetMethod.Name}_GeneratedTests";
        var namespaceName = targetType.Namespace ?? "GeneratedTests";

        var testMethods = explorationResult.TestCases
            .Select((tc, index) => GenerateTestMethod(targetType, targetMethod, tc, index))
            .ToArray();

        var classDeclaration = ClassDeclaration(className)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddMembers(testMethods);

        var namespaceDeclaration = FileScopedNamespaceDeclaration(ParseName(namespaceName))
            .AddMembers(classDeclaration);

        var compilationUnit = CompilationUnit()
            .AddUsings(
                UsingDirective(ParseName("System")),
                UsingDirective(ParseName("Xunit"))
            )
            .AddMembers(namespaceDeclaration)
            .NormalizeWhitespace();

        return compilationUnit.ToFullString();
    }

    private MemberDeclarationSyntax GenerateTestMethod(
        Type targetType,
        MethodInfo targetMethod,
        TestCase testCase,
        int index)
    {
        var methodName = $"Test_{targetMethod.Name}_{index}";

        // Generate method body
        var statements = new List<StatementSyntax>();

        // Arrange - create instance if needed
        if (!targetMethod.IsStatic)
        {
            statements.Add(ParseStatement($"var instance = new {targetType.Name}();"));
        }

        // Act - call the method
        var arguments = string.Join(", ", testCase.Inputs.Select(FormatValue));
        var invocation = targetMethod.IsStatic
            ? $"{targetType.Name}.{targetMethod.Name}({arguments})"
            : $"instance.{targetMethod.Name}({arguments})";

        if (targetMethod.ReturnType != typeof(void))
        {
            statements.Add(ParseStatement($"var result = {invocation};"));
            statements.Add(ParseStatement("Assert.NotNull(result);"));
        }
        else
        {
            statements.Add(ParseStatement($"{invocation};"));
        }

        var methodDeclaration = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                methodName)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(
                AttributeList(SingletonSeparatedList(
                    Attribute(IdentifierName("Fact"))))
            )
            .WithBody(Block(statements));

        return methodDeclaration;
    }

    private string FormatValue(KeyValuePair<string, object?> input)
    {
        return input.Value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            _ => input.Value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Generates a single test method as a string
    /// </summary>
    public string GenerateTestMethodString(
        Type targetType,
        MethodInfo targetMethod,
        TestCase testCase,
        int index)
    {
        var method = GenerateTestMethod(targetType, targetMethod, testCase, index);
        return method.NormalizeWhitespace().ToFullString();
    }
}
