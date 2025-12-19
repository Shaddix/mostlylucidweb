using Bogus;
using Mostlylucid.DataSummarizer.Models;

namespace Mostlylucid.DataSummarizer.Services;

public static class DataSynthesizer
{
    public static void GenerateCsv(DataProfile profile, int rows, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        using var writer = new StreamWriter(outputPath);
        writer.WriteLine(string.Join(',', profile.Columns.Select(c => Escape(c.Name))));

        var rnd = new Random();
        for (int i = 0; i < rows; i++)
        {
            var row = profile.Columns.Select(col => GenerateValue(col, rnd, i)).Select(Escape);
            writer.WriteLine(string.Join(',', row));
        }
    }

    private static string GenerateValue(ColumnProfile col, Random rnd, int rowIndex)
    {
        switch (col.InferredType)
        {
            case ColumnType.Id:
                return (rowIndex + 1).ToString();
            case ColumnType.Numeric:
                {
                    var mean = col.Mean ?? 0;
                    var std = col.StdDev ?? Math.Max(1, (col.Max ?? mean + 1) - (col.Min ?? mean - 1)) / 6.0;
                    var val = NextGaussian(rnd, mean, std);
                    if (col.Min.HasValue && col.Max.HasValue)
                        val = Math.Clamp(val, col.Min.Value, col.Max.Value);
                    return Math.Round(val, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            case ColumnType.DateTime:
                {
                    var min = col.MinDate ?? DateTime.UtcNow.AddDays(-30);
                    var max = col.MaxDate ?? DateTime.UtcNow;
                    if (max <= min) max = min.AddDays(1);
                    var span = max - min;
                    var t = rnd.NextDouble();
                    var val = min + TimeSpan.FromTicks((long)(span.Ticks * t));
                    return val.ToString("o");
                }
            case ColumnType.Boolean:
                return rnd.NextDouble() < 0.5 ? "false" : "true";
            case ColumnType.Categorical:
                {
                    if (col.TopValues?.Count > 0)
                    {
                        // Weighted pick by counts
                        var total = col.TopValues.Sum(v => v.Count);
                        var pick = rnd.NextDouble() * total;
                        double acc = 0;
                        foreach (var v in col.TopValues)
                        {
                            acc += v.Count;
                            if (pick <= acc) return v.Value;
                        }
                        return col.TopValues[0].Value;
                    }
                    return new Faker().Commerce.Department();
                }
            case ColumnType.Text:
                {
                    var faker = new Faker();
                    var len = col.AvgLength.HasValue ? Math.Max(3, (int)Math.Round(col.AvgLength.Value)) : 12;
                    return faker.Random.AlphaNumeric(Math.Min(len, 50));
                }
            default:
                return "";
        }
    }

    private static double NextGaussian(Random rnd, double mean, double stddev)
    {
        // Box-Muller
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = 1.0 - rnd.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stddev * randStdNormal;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
