using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Output;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.UnitTests.Insights;

[TestFixture]
public class CursorTests
{
    [Test]
    public void DecodeOffset_RoundTripsEncodedOffset()
    {
        Cursor.DecodeOffset(Cursor.EncodeOffset(1234)).ShouldBe(1234);
    }

    [Test]
    public void DecodeOffset_NoCursor_ReturnsZero()
    {
        Cursor.DecodeOffset(null).ShouldBe(0);
    }

    [Test]
    public void DecodeKeyset_RoundTripsTimestampAndId()
    {
        var at = new DateTime(2026, 3, 18, 12, 0, 0, 123, DateTimeKind.Utc);

        var decoded = Cursor.DecodeKeyset(Cursor.EncodeKeyset(at, "abc|def"));

        decoded.ShouldBe((at, "abc|def"));
    }

    [Test]
    public void DecodeOffset_KeysetCursor_ThrowsNamingCursor()
    {
        var ex = Should.Throw<InsightsArgumentException>(() =>
            Cursor.DecodeOffset(Cursor.EncodeKeyset(DateTime.UtcNow, "x")));

        ex.ArgumentName.ShouldBe("cursor");
    }

    [Test]
    public void DecodeOffset_Garbage_ThrowsNamingCursor()
    {
        Should.Throw<InsightsArgumentException>(() => Cursor.DecodeOffset("!!not base64!!"))
            .ArgumentName.ShouldBe("cursor");
    }

    [TestCase(0, 10, 25, true)]
    [TestCase(10, 10, 20, false)]
    [TestCase(0, 0, 0, false)]
    public void NextOffset_OnlyWhenRowsRemain(int offset, int returned, int total, bool hasNext)
    {
        (Cursor.NextOffset(offset, returned, total) != null).ShouldBe(hasNext);
    }
}

[TestFixture]
public class SortSpecTests
{
    private static readonly string[] Allowed = ["total_ms", "runs"];
    private static readonly SortSpec Default = new("runs", true);

    [TestCase("total_ms desc", true)]
    [TestCase("total_ms asc", false)]
    [TestCase("-total_ms", true)]
    [TestCase("total_ms", true)]
    [TestCase("TOTAL_MS ASC", false)]
    public void Parse_AcceptsSupportedForms(string sort, bool descending)
    {
        SortSpec.Parse(sort, Allowed, Default).ShouldBe(new SortSpec("total_ms", descending));
    }

    [Test]
    public void Parse_Blank_ReturnsDefault()
    {
        SortSpec.Parse(" ", Allowed, Default).ShouldBe(Default);
    }

    [Test]
    public void Parse_UnknownColumn_ThrowsListingAllowedColumns()
    {
        var ex = Should.Throw<InsightsArgumentException>(() => SortSpec.Parse("user_name", Allowed, Default));

        ex.ArgumentName.ShouldBe("sort");
        ex.Message.ShouldContain("total_ms");
    }

    [Test]
    public void Parse_BadDirection_Throws()
    {
        Should.Throw<InsightsArgumentException>(() => SortSpec.Parse("runs sideways", Allowed, Default));
    }
}

[TestFixture]
public class InsightsScopeTests
{
    private static readonly DateTime Now = new(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Resolve_NoArgs_Uses14DaysAndAllEnvironments()
    {
        var scope = InsightsScope.Resolve(new InsightsArgs(), Now, 90);

        scope.Window.ShouldBe(new InsightsWindow(Now.AddDays(-14), Now));
        scope.Environment.ShouldBe("all");
        scope.StoredEnvironment.ShouldBeNull();
    }

    [Test]
    public void Resolve_DaysBeyondRetention_IsCappedAtRetention()
    {
        var scope = InsightsScope.Resolve(new InsightsArgs().With("days", 365), Now, 90);

        scope.Window.From.ShouldBe(Now.AddDays(-90));
    }

    [Test]
    public void Resolve_FromAndTo_OverrideDays()
    {
        var args = new InsightsArgs().With("days", 3).With("from", "2026-03-01T00:00:00Z").With("to", "2026-03-05T00:00:00Z");

        var scope = InsightsScope.Resolve(args, Now, 90);

        scope.Window.ShouldBe(new InsightsWindow(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc)));
    }

    [TestCase("CI", "CI")]
    [TestCase("local", "Local")]
    public void Resolve_Environment_MapsToStoredValue(string environment, string stored)
    {
        InsightsScope.Resolve(new InsightsArgs().With("environment", environment), Now, 90)
            .StoredEnvironment.ShouldBe(stored);
    }

    [Test]
    public void Resolve_DefaultEnvironment_AppliesWhenNotGiven()
    {
        InsightsScope.Resolve(new InsightsArgs(), Now, 90, defaultEnvironment: InsightsScope.ENV_CI)
            .Environment.ShouldBe("ci");
    }

    [Test]
    public void Resolve_UnknownEnvironment_ThrowsNamingEnvironment()
    {
        Should.Throw<InsightsArgumentException>(() =>
                InsightsScope.Resolve(new InsightsArgs().With("environment", "staging"), Now, 90))
            .ArgumentName.ShouldBe("environment");
    }

    [Test]
    public void Resolve_ZeroDays_Throws()
    {
        Should.Throw<InsightsArgumentException>(() =>
            InsightsScope.Resolve(new InsightsArgs().With("days", 0), Now, 90));
    }
}

[TestFixture]
public class InsightsArgsTests
{
    [Test]
    public void With_BlankValue_RemovesArgument()
    {
        var args = new InsightsArgs().With("a", "x").With("a", " ");

        args.Has("a").ShouldBeFalse();
    }

    [Test]
    public void GetInt_NotANumber_ThrowsNamingArgument()
    {
        Should.Throw<InsightsArgumentException>(() => new InsightsArgs().With("min_runs", "many").GetInt("min_runs"))
            .ArgumentName.ShouldBe("min_runs");
    }

    [Test]
    public void GetList_SplitsAndTrims()
    {
        new InsightsArgs().With("group_by", " day, cpu_count ,,").GetList("group_by").ShouldBe(["day", "cpu_count"]);
    }

    [Test]
    public void ToQueryString_SortsAndEscapes()
    {
        var args = new InsightsArgs().With("suite_id", "ab12").With("branch", "feature/x y").With("min_runs", 3);

        args.ToQueryString().ShouldBe("branch=feature%2Fx%20y&min_runs=3&suite_id=ab12");
    }

    [Test]
    public void ToQueryString_Excludes_SkipsPagingArgs()
    {
        var args = new InsightsArgs().With("limit", 5).With("days", 7);

        args.ToQueryString(DatasetNames.PagingArgs).ShouldBe("days=7");
    }
}

[TestFixture]
public class IdentityTests
{
    [Test]
    public void Suite_IsStableAndEightHexCharacters()
    {
        var id = StableId.Suite("web-app", "Web.Tests", "NUnit");

        id.ShouldBe(StableId.Suite("web-app", "Web.Tests", "NUnit"));
        id.ShouldMatch("^[0-9a-f]{8}$");
    }

    [Test]
    public void Suite_DiffersByRunner()
    {
        StableId.Suite("web-app", "Web.Tests", "NUnit").ShouldNotBe(StableId.Suite("web-app", "Web.Tests", "Vitest"));
    }

    [Test]
    public void Series_NullAndEmptyReloadType_AreTheSameSeries()
    {
        StableId.Series("r", "p", ".Net", null).ShouldBe(StableId.Series("r", "p", ".Net", ""));
    }

    [TestCase("Full.Name", "Cls", "N", "Full.Name")]
    [TestCase(null, "Cls", "N", "Cls.N")]
    [TestCase("", "Cls", "N", "Cls.N")]
    [TestCase(null, null, "N", "N")]
    public void NameKey_PrefersFullNameThenClassAndName(string? fullName, string? className, string name, string expected)
    {
        TestKey.NameKey(fullName, className, name).ShouldBe(expected);
    }

    [Test]
    public void TryParse_SplitsAtFirstColon()
    {
        TestKey.TryParse("ab12cd34:Ns.Cls.Test(\"a:b\")", out var suite, out var name).ShouldBeTrue();

        suite.ShouldBe("ab12cd34");
        name.ShouldBe("Ns.Cls.Test(\"a:b\")");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nocolon")]
    [TestCase(":name")]
    [TestCase("suite:")]
    public void TryParse_Malformed_ReturnsFalse(string? key)
    {
        TestKey.TryParse(key, out _, out _).ShouldBeFalse();
    }

    [Test]
    public void FirstLine_TakesFirstLineAndCapsAt300()
    {
        ErrorText.FirstLine("  boom\r\nstack trace").ShouldBe("boom");
        ErrorText.FirstLine(new string('x', 400))!.Length.ShouldBe(300);
        ErrorText.FirstLine("  ").ShouldBeNull();
    }
}

[TestFixture]
public class StatsTests
{
    [Test]
    public void Percentile_InterpolatesLinearly()
    {
        double[] values = [400, 100, 300, 200];

        Stats.Percentile(values, 0.5).ShouldBe(250);
        Stats.Percentile(values, 0.95)!.Value.ShouldBe(385, 0.0001);
    }

    [Test]
    public void Percentile_SingleValue_ReturnsIt()
    {
        Stats.Percentile([42.0], 0.95).ShouldBe(42);
    }

    [Test]
    public void Percentile_NoValues_ReturnsNull()
    {
        Stats.Percentile([], 0.5).ShouldBeNull();
    }

    [Test]
    public void Ms_RoundsHalfAwayFromZero()
    {
        Stats.Ms(2.5).ShouldBe(3);
        Stats.Ms(null).ShouldBeNull();
    }

    [Test]
    public void Ratio_ZeroDenominator_ReturnsNull()
    {
        Stats.Ratio(1, 0).ShouldBeNull();
        Stats.Ratio(1, 3).ShouldBe(0.3333);
    }
}

[TestFixture]
public class TableFormatterTests
{
    private static readonly Table Sample = new(
        ["name", "runs", "rate", "at", "flag", "missing"],
        [["a, \"b\"\nc", 3L, 0.12346, new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc), true, null]],
        1,
        null);

    [Test]
    public void ToCsv_QuotesFieldsWithCommasQuotesAndNewlines()
    {
        TableFormatter.ToCsv(Sample).ShouldBe(
            "name,runs,rate,at,flag,missing\r\n" +
            "\"a, \"\"b\"\"\nc\",3,0.1235,2026-03-18T12:00:00Z,true,\r\n");
    }

    [Test]
    public void ToJson_WritesTypedValuesKeyedByColumn()
    {
        TableFormatter.ToJson(Sample).ShouldBe(
            "[{\"name\":\"a, \\u0022b\\u0022\\nc\",\"runs\":3,\"rate\":0.1235,\"at\":\"2026-03-18T12:00:00Z\",\"flag\":true,\"missing\":null}]");
    }

    [Test]
    public void ToCsv_EmptyTable_WritesHeaderOnly()
    {
        TableFormatter.ToCsv(Table.Empty(["a", "b"])).ShouldBe("a,b\r\n");
    }
}
