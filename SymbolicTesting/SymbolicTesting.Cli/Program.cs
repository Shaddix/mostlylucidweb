using System.CommandLine;
using System.Reflection;
using SymbolicTesting.Core;
using SymbolicTesting.TestGeneration;

namespace SymbolicTesting.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Symbolic Testing Framework - Automatically generate tests using symbolic execution");

        var assemblyOption = new Option<FileInfo>(
            name: "--assembly",
            description: "Path to the assembly to analyze")
        {
            IsRequired = true
        };

        var typeOption = new Option<string>(
            name: "--type",
            description: "Fully qualified type name")
        {
            IsRequired = true
        };

        var methodOption = new Option<string>(
            name: "--method",
            description: "Method name to analyze")
        {
            IsRequired = true
        };

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "Output file for generated tests",
            getDefaultValue: () => new FileInfo("GeneratedTests.cs"));

        var maxPathsOption = new Option<int>(
            name: "--max-paths",
            description: "Maximum number of paths to explore",
            getDefaultValue: () => 100);

        var timeoutOption = new Option<int>(
            name: "--timeout",
            description: "Timeout in seconds",
            getDefaultValue: () => 30);

        rootCommand.AddOption(assemblyOption);
        rootCommand.AddOption(typeOption);
        rootCommand.AddOption(methodOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(maxPathsOption);
        rootCommand.AddOption(timeoutOption);

        rootCommand.SetHandler(async (assembly, typeName, methodName, output, maxPaths, timeout) =>
        {
            await RunSymbolicExecution(assembly, typeName, methodName, output, maxPaths, timeout);
        }, assemblyOption, typeOption, methodOption, outputOption, maxPathsOption, timeoutOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunSymbolicExecution(
        FileInfo assemblyFile,
        string typeName,
        string methodName,
        FileInfo outputFile,
        int maxPaths,
        int timeout)
    {
        try
        {
            Console.WriteLine("Symbolic Testing Framework");
            Console.WriteLine("==========================");
            Console.WriteLine();
            Console.WriteLine($"Assembly: {assemblyFile.FullName}");
            Console.WriteLine($"Type: {typeName}");
            Console.WriteLine($"Method: {methodName}");
            Console.WriteLine();

            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyFile.FullName);

            // Find the type
            var type = assembly.GetType(typeName);
            if (type == null)
            {
                Console.Error.WriteLine($"Error: Type '{typeName}' not found in assembly");
                return;
            }

            // Find the method
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method == null)
            {
                Console.Error.WriteLine($"Error: Method '{methodName}' not found in type '{typeName}'");
                return;
            }

            Console.WriteLine("Starting symbolic execution...");
            Console.WriteLine();

            // Run symbolic execution
            var executor = new SymbolicExecutor();
            var options = new ExplorationOptions
            {
                MaxPaths = maxPaths,
                Timeout = TimeSpan.FromSeconds(timeout)
            };

            var result = await executor.ExploreAsync(method, options);

            Console.WriteLine("Exploration Results:");
            Console.WriteLine($"  Paths explored: {result.PathsExplored}");
            Console.WriteLine($"  Satisfiable paths: {result.PathsSatisfiable}");
            Console.WriteLine($"  Test cases generated: {result.TestCases.Count}");
            Console.WriteLine($"  Execution time: {result.ExecutionTime.TotalSeconds:F2}s");
            Console.WriteLine();

            // Generate test code
            var generator = new TestGenerator();
            var testCode = generator.GenerateTestClass(type, method, result);

            // Write to output file
            await File.WriteAllTextAsync(outputFile.FullName, testCode);

            Console.WriteLine($"Generated tests written to: {outputFile.FullName}");
            Console.WriteLine();
            Console.WriteLine("Test Cases:");

            foreach (var testCase in result.TestCases)
            {
                var inputs = string.Join(", ", testCase.Inputs.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                Console.WriteLine($"  - {testCase.MethodName}({inputs})");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }
}
