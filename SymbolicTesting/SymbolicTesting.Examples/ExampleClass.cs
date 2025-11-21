namespace SymbolicTesting.Examples;

/// <summary>
/// Example class to demonstrate symbolic testing
/// </summary>
public class Calculator
{
    /// <summary>
    /// Simple method with branches - perfect for symbolic execution
    /// </summary>
    public static string Classify(int number)
    {
        if (number < 0)
            return "negative";
        else if (number == 0)
            return "zero";
        else if (number < 10)
            return "small positive";
        else
            return "large positive";
    }

    /// <summary>
    /// Method with arithmetic operations
    /// </summary>
    public static int Compute(int x, int y)
    {
        if (x > y)
            return x * 2 + y;
        else if (x == y)
            return x + y;
        else
            return y * 2 - x;
    }

    /// <summary>
    /// Method with nested conditions
    /// </summary>
    public static bool IsValid(int age, bool hasLicense)
    {
        if (age >= 18)
        {
            if (hasLicense)
                return true;
            else
                return false;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Method that finds maximum
    /// </summary>
    public static int Max(int a, int b, int c)
    {
        int max = a;

        if (b > max)
            max = b;

        if (c > max)
            max = c;

        return max;
    }

    /// <summary>
    /// Method with division (can find divide-by-zero)
    /// </summary>
    public static int Divide(int numerator, int denominator)
    {
        if (denominator == 0)
            throw new DivideByZeroException("Cannot divide by zero");

        return numerator / denominator;
    }
}

/// <summary>
/// Example with more complex logic
/// </summary>
public class StringAnalyzer
{
    public static string AnalyzeLength(string? input)
    {
        if (input == null)
            return "null";

        int length = input.Length;

        if (length == 0)
            return "empty";
        else if (length < 5)
            return "short";
        else if (length < 20)
            return "medium";
        else
            return "long";
    }
}
