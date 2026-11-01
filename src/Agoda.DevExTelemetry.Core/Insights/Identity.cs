using System.Security.Cryptography;
using System.Text;

namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>Short, stable ids for things the schema has no key for: test suites and build series.</summary>
public static class StableId
{
    /// <summary>
    /// 8 lowercase hex characters of the SHA-256 of the parts. Null and empty parts hash the same,
    /// so a missing <c>ReloadType</c> and an empty one give the same series.
    /// </summary>
    public static string Compute(params string?[] parts)
    {
        var joined = string.Join('\u001f', parts.Select(p => p ?? string.Empty));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    /// <summary>A test suite: repository (RepositoryName) + project (ProjectName) + test runner.</summary>
    public static string Suite(string repository, string project, string testRunner) =>
        Compute("suite", repository, project, testRunner);

    /// <summary>A build series: repository (RepositoryName) + project (ProjectName) + metric type + reload type.</summary>
    public static string Series(string repository, string project, string metricType, string? reloadType) =>
        Compute("series", repository, project, metricType, reloadType);
}

/// <summary>
/// Test identity (decision D5): suite + <c>COALESCE(FullName, ClassName + "." + Name)</c>.
/// The combined key is <c>{suite_id}:{name_key}</c>, for example <c>ab12cd34:Namespace.Class.Test_1</c>.
/// </summary>
public static class TestKey
{
    /// <summary>The name part of the key. Mirrors the SQL expression <c>FullName ?? (ClassName + "." + Name)</c>.</summary>
    public static string NameKey(string? fullName, string? className, string name) =>
        !string.IsNullOrEmpty(fullName)
            ? fullName
            : string.IsNullOrEmpty(className) ? name : className + "." + name;

    public static string Format(string suiteId, string nameKey) => suiteId + ":" + nameKey;

    /// <summary>Splits a combined key into suite id and name key.</summary>
    public static bool TryParse(string? testKey, out string suiteId, out string nameKey)
    {
        suiteId = string.Empty;
        nameKey = string.Empty;
        if (string.IsNullOrEmpty(testKey)) return false;
        var colon = testKey.IndexOf(':');
        if (colon <= 0 || colon == testKey.Length - 1) return false;
        suiteId = testKey[..colon];
        nameKey = testKey[(colon + 1)..];
        return true;
    }
}

/// <summary>Error messages as the output format wants them: first line only, at most 300 characters.</summary>
public static class ErrorText
{
    public const int MAX_LENGTH = 300;

    public static string? FirstLine(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return null;
        var line = error.Trim().Split('\n', 2)[0].TrimEnd('\r').Trim();
        return line.Length <= MAX_LENGTH ? line : line[..MAX_LENGTH];
    }
}

/// <summary>Shared statistics so every tool computes percentiles and rounding the same way.</summary>
public static class Stats
{
    /// <summary>
    /// Percentile by linear interpolation between closest ranks (the same as PostgreSQL
    /// <c>percentile_cont</c>). <paramref name="p"/> is 0..1. Returns null for no values.
    /// </summary>
    public static double? Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return null;
        if (sorted.Length == 1) return sorted[0];
        var rank = p * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    /// <summary>Whole milliseconds, rounded half away from zero.</summary>
    public static long? Ms(double? value) =>
        value.HasValue ? (long)Math.Round(value.Value, MidpointRounding.AwayFromZero) : null;

    /// <summary>A ratio rounded to 4 decimal places, or null when the denominator is 0.</summary>
    public static double? Ratio(double numerator, double denominator) =>
        denominator == 0 ? null : Math.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);
}
