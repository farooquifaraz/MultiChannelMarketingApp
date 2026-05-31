using System.Globalization;
using System.Text;

namespace MarketingApp.API.Helpers;

/// <summary>
/// Minimal RFC4180-compliant CSV writer with no external deps.
/// We're not using CsvHelper to keep dep footprint small — these exports are
/// straightforward row → string projections.
/// </summary>
public static class CsvExporter
{
    public static byte[] BuildCsv<T>(IEnumerable<T> rows, IEnumerable<(string Header, Func<T, object?> Selector)> columns)
    {
        var sb = new StringBuilder();
        var cols = columns.ToList();

        // Header
        sb.AppendLine(string.Join(",", cols.Select(c => EscapeField(c.Header))));

        // Rows
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", cols.Select(c => EscapeField(FormatValue(c.Selector(row))))));
        }

        // UTF-8 BOM so Excel opens it with proper encoding
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new byte[] { 0xEF, 0xBB, 0xBF }.Concat(bytes).ToArray();
    }

    private static string EscapeField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuoting) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
