/*
 * ⚠️ TRIVIAL OBFUSCATION DEMO ⚠️
 * This is a simplified demonstration. Production would use:
 * - Proper JavaScript minifiers (Terser, UglifyJS)
 * - AST manipulation for code transformation
 * - Custom encryption schemes
 * - Dead code injection
 * - Control flow flattening
 */

using System.Text;
using System.Text.RegularExpressions;

namespace Mostlylucid.SecureChat.Demo.Build;

public class JsObfuscator
{
    /// <summary>
    /// Converts a string to String.fromCharCode() representation
    /// </summary>
    public static string StringToCharCodes(string input)
    {
        var codes = input.Select(c => ((int)c).ToString());
        return $"String.fromCharCode({string.Join(",", codes)})";
    }

    /// <summary>
    /// Simple XOR encoding (demo only - not cryptographically secure)
    /// </summary>
    public static string XorEncode(string input, int key)
    {
        var encoded = input.Select(c => (char)(c ^ key)).ToArray();
        var codes = encoded.Select(c => ((int)c).ToString());
        return $"String.fromCharCode({string.Join(",", codes)})";
    }

    /// <summary>
    /// Split string into chunks and reassemble at runtime
    /// </summary>
    public static string SplitString(string input)
    {
        var chunks = new List<string>();
        for (int i = 0; i < input.Length; i += 3)
        {
            var chunk = input.Substring(i, Math.Min(3, input.Length - i));
            chunks.Add($"\"{chunk}\"");
        }
        return $"[{string.Join(",", chunks)}].join('')";
    }

    /// <summary>
    /// Obfuscate string literals in JavaScript code
    /// This is a VERY simplified version for demonstration
    /// </summary>
    public static string ObfuscateStringLiterals(string jsCode)
    {
        // Find string literals in single or double quotes
        var pattern = @"[""']([^""'\\]*(\\.[^""'\\]*)*)[""']";

        return Regex.Replace(jsCode, pattern, match =>
        {
            var original = match.Value;
            var content = match.Groups[1].Value;

            // Skip very short strings (might be operators, etc.)
            if (content.Length < 3)
                return original;

            // Skip regex patterns
            if (match.Index > 0 && jsCode[match.Index - 1] == '/')
                return original;

            // Convert to fromCharCode
            return StringToCharCodes(content);
        });
    }

    /// <summary>
    /// Basic variable name obfuscation
    /// </summary>
    public static string ObfuscateVariableNames(string jsCode)
    {
        var varMap = new Dictionary<string, string>();
        var counter = 0;

        // Find variable declarations
        var pattern = @"\b(const|let|var)\s+(\w+)\b";

        return Regex.Replace(jsCode, pattern, match =>
        {
            var keyword = match.Groups[1].Value;
            var varName = match.Groups[2].Value;

            // Skip common names that might be used elsewhere
            if (varName.Length <= 2)
                return match.Value;

            if (!varMap.ContainsKey(varName))
            {
                varMap[varName] = $"_{GenerateVarName(counter++)}";
            }

            return $"{keyword} {varMap[varName]}";
        });
    }

    private static string GenerateVarName(int index)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = new StringBuilder();

        do
        {
            result.Insert(0, chars[index % chars.Length]);
            index /= chars.Length;
        } while (index > 0);

        return result.ToString();
    }

    /// <summary>
    /// Add junk code for obfuscation
    /// </summary>
    public static string AddDeadCode(string jsCode)
    {
        var junk = new[]
        {
            "var _0x1a2b=function(){};",
            "!function(){var _=0;}();",
            "const __x=null;",
        };

        return string.Join("", junk) + jsCode;
    }

    /// <summary>
    /// Simple minification (removes comments and extra whitespace)
    /// Real production would use proper tools like Terser
    /// </summary>
    public static string Minify(string jsCode)
    {
        // Remove single-line comments
        jsCode = Regex.Replace(jsCode, @"//.*$", "", RegexOptions.Multiline);

        // Remove multi-line comments
        jsCode = Regex.Replace(jsCode, @"/\*[\s\S]*?\*/", "");

        // Remove extra whitespace
        jsCode = Regex.Replace(jsCode, @"\s+", " ");

        // Remove spaces around operators and punctuation
        jsCode = Regex.Replace(jsCode, @"\s*([{}()\[\];,=<>!+\-*/&|])\s*", "$1");

        return jsCode.Trim();
    }

    /// <summary>
    /// Full obfuscation pipeline (demo version)
    /// </summary>
    public static string Obfuscate(string jsCode, bool aggressive = false)
    {
        if (aggressive)
        {
            // More aggressive obfuscation
            jsCode = ObfuscateStringLiterals(jsCode);
            jsCode = AddDeadCode(jsCode);
        }

        jsCode = Minify(jsCode);

        return jsCode;
    }
}
