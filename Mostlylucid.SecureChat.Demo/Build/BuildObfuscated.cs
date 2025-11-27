/*
 * Build tool to generate obfuscated JavaScript files
 * Run this to create production versions of the JS files
 */

using System.Text;

namespace Mostlylucid.SecureChat.Demo.Build;

public class BuildObfuscated
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🔨 Building obfuscated JavaScript files...\n");

        var projectRoot = FindProjectRoot();
        var devPath = Path.Combine(projectRoot, "wwwroot", "js", "dev");
        var outputPath = Path.Combine(projectRoot, "wwwroot", "js");

        // Build compatibility shim (aggressive obfuscation)
        Console.WriteLine("Building compatibility-shim1.js...");
        BuildShim(devPath, outputPath);

        // Build secure chat (moderate obfuscation for readability in demo)
        Console.WriteLine("Building secure-chat.js...");
        BuildSecureChat(devPath, outputPath);

        Console.WriteLine("\n✅ Build complete!");
        Console.WriteLine($"📁 Output: {outputPath}");
        Console.WriteLine("\n⚠️  Remember: These are DEMO obfuscations.");
        Console.WriteLine("    Production would use proper tools like Terser, Webpack, etc.");
    }

    private static void BuildShim(string devPath, string outputPath)
    {
        var sourcePath = Path.Combine(devPath, "compatibility-shim1.src.js");
        var source = File.ReadAllText(sourcePath);

        // For the shim, we want maximum compression
        var obfuscated = JsObfuscator.Obfuscate(source, aggressive: true);

        // Add warning comment
        var output = new StringBuilder();
        output.AppendLine("/*");
        output.AppendLine(" * ⚠️ TRIVIAL OBFUSCATION - DEMO ONLY ⚠️");
        output.AppendLine(" * Real version would use proper minification and custom encryption.");
        output.AppendLine(" * This demonstrates the concept with simple string splitting.");
        output.AppendLine(" */");
        output.Append(obfuscated);

        var outputFile = Path.Combine(outputPath, "compatibility-shim1.js");
        File.WriteAllText(outputFile, output.ToString());

        var originalSize = new FileInfo(sourcePath).Length;
        var obfuscatedSize = new FileInfo(outputFile).Length;

        Console.WriteLine($"  Source: {originalSize} bytes");
        Console.WriteLine($"  Output: {obfuscatedSize} bytes ({(double)obfuscatedSize / originalSize * 100:F1}% of original)");
    }

    private static void BuildSecureChat(string devPath, string outputPath)
    {
        var sourcePath = Path.Combine(devPath, "secure-chat.src.js");
        var source = File.ReadAllText(sourcePath);

        // For the chat, moderate obfuscation (keep some readability for demo)
        var obfuscated = JsObfuscator.Minify(source);

        // Add warning comment
        var output = new StringBuilder();
        output.AppendLine("/*");
        output.AppendLine(" * ⚠️ DEMO VERSION - NOT ENCRYPTED ⚠️");
        output.AppendLine(" * Production version would have:");
        output.AppendLine(" * - Full string encryption");
        output.AppendLine(" * - Control flow obfuscation");
        output.AppendLine(" * - Dead code injection");
        output.AppendLine(" * - Anti-debugging measures");
        output.AppendLine(" */");
        output.Append(obfuscated);

        var outputFile = Path.Combine(outputPath, "secure-chat.js");
        File.WriteAllText(outputFile, output.ToString());

        var originalSize = new FileInfo(sourcePath).Length;
        var obfuscatedSize = new FileInfo(outputFile).Length;

        Console.WriteLine($"  Source: {originalSize} bytes");
        Console.WriteLine($"  Output: {obfuscatedSize} bytes ({(double)obfuscatedSize / originalSize * 100:F1}% of original)");
    }

    private static string FindProjectRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(current, "Program.cs")))
        {
            var parent = Directory.GetParent(current);
            if (parent == null)
                throw new Exception("Could not find project root");
            current = parent.FullName;
        }
        return current;
    }
}
