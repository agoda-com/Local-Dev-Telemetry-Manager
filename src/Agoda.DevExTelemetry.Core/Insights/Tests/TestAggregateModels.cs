namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>How <c>get_tests</c> groups executions.</summary>
public enum TestGrouping
{
    /// <summary>One row per suite and test name key.</summary>
    Test,

    /// <summary>One row per suite and class name; a null or empty class becomes <see cref="TestsFilter.NO_CLASS"/>.</summary>
    Class
}

/// <summary>Group-level status filters of <c>get_tests</c>, applied to the aggregated counts.</summary>
public enum TestStatusFilter
{
    None,

    /// <summary>failed &gt; 0</summary>
    HasFailures,

    /// <summary>passed &gt; 0 and failed &gt; 0</summary>
    HasPassesAndFailures,

    /// <summary>The latest execution (or, for classes, the latest run) failed.</summary>
    LastFailed,

    /// <summary>failed &gt; 0 and passed == 0</summary>
    AlwaysFailed
}

/// <summary>
/// Filters for <see cref="ITestAggregateQueries.QueryTestsAsync"/>. All are optional. The text filters apply to
/// individual executions before grouping; <see cref="MinRuns"/>, <see cref="MinAvgMs"/> and <see cref="Status"/>
/// apply to the aggregated groups.
/// </summary>
public sealed record TestsFilter
{
    public const string NO_CLASS = "(none)";

    public TestGrouping GroupBy { get; init; } = TestGrouping.Test;

    /// <summary>Only this suite.</summary>
    public TestSuiteIdentity? Suite { get; init; }

    public string? TestRunner { get; init; }

    /// <summary>Case-insensitive; matches Name or NameKey.</summary>
    public string? NameContains { get; init; }

    /// <summary>Case-insensitive; matches ClassName.</summary>
    public string? ClassContains { get; init; }

    /// <summary>
    /// Only these name keys (test grouping only). Used to enrich the rows of one page without aggregating every
    /// test; the text filters still apply to the executions, so a page row aggregates exactly as it did unfiltered.
    /// </summary>
    public IReadOnlyCollection<string>? NameKeys { get; init; }

    public int? MinRuns { get; init; }

    public double? MinAvgMs { get; init; }

    public TestStatusFilter Status { get; init; } = TestStatusFilter.None;
}

/// <summary>
/// One aggregated test (or class) over the window. <see cref="TestKey"/> is <c>suite_id:name_key</c>, or
/// <c>suite_id:class_name</c> when grouped by class. <see cref="StatusSequence"/> holds up to the last 30
/// results, oldest first, one letter each: P, F, S or ?.
/// </summary>
public sealed record TestAggregateRow
{
    public string TestKey { get; init; } = string.Empty;
    public string SuiteId { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string TestRunner { get; init; } = string.Empty;
    public string? ClassName { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Runs { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public double? FailRate { get; init; }
    public long? AvgMs { get; init; }
    public long? P50Ms { get; init; }
    public long? P95Ms { get; init; }
    public long? MaxMs { get; init; }
    public long? TotalMs { get; init; }
    public DateTime FirstSeenAt { get; init; }
    public DateTime LastRunAt { get; init; }
    public string LastStatus { get; init; } = string.Empty;
    public DateTime? LastFailedAt { get; init; }
    public string StatusSequence { get; init; } = string.Empty;
}

/// <summary>One test suite over the window.</summary>
public sealed record TestSuiteRow
{
    public string SuiteId { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string TestRunner { get; init; } = string.Empty;
    public int Runs { get; init; }
    public int CiRuns { get; init; }
    public int LocalRuns { get; init; }
    public DateTime LastRunAt { get; init; }
    public int? TestsInLastRun { get; init; }
    public long? AvgRunMs { get; init; }
    public long? P95RunMs { get; init; }
    public int FailedRuns { get; init; }
    public int DistinctTests { get; init; }
    public int Branches { get; init; }
}
