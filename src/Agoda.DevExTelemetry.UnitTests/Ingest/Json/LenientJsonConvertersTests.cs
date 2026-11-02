using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Models.Ingest.Json;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.UnitTests.Ingest.Json;

[TestFixture]
public class LenientJsonConvertersTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public class Probe
    {
        [JsonPropertyName("platform")]
        [JsonConverter(typeof(LenientPlatformConverter))]
        public PlatformID Platform { get; set; }

        [JsonPropertyName("timeTaken")]
        [JsonConverter(typeof(NumberOrStringConverter))]
        public string? TimeTaken { get; set; }

        [JsonPropertyName("builtAt")]
        [JsonConverter(typeof(EpochOrIsoConverter))]
        public long? BuiltAt { get; set; }

        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(EpochOrIsoConverter))]
        public long Timestamp { get; set; }

        [JsonPropertyName("errorMessage")]
        [JsonConverter(typeof(StringOrStringArrayConverter))]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("cpuSpeed")]
        [JsonConverter(typeof(NumberOrNumberArrayConverter))]
        public List<double>? CpuSpeed { get; set; }
    }

    private static IEnumerable<TestCaseData> DocumentedForms()
    {
        TestCaseData Form(string name, string json, Func<Probe, object?> read, object? expected) =>
            new TestCaseData(json, read, expected).SetName($"Converters_AcceptEveryDocumentedForm({name})");

        yield return Form("platform int", """{"platform":4}""", p => p.Platform, PlatformID.Unix);
        yield return Form("platform Linux", """{"platform":"Linux"}""", p => p.Platform, PlatformID.Unix);
        yield return Form("platform Darwin", """{"platform":"Darwin"}""", p => p.Platform, PlatformID.MacOSX);
        yield return Form("platform Windows_NT", """{"platform":"Windows_NT"}""", p => p.Platform, PlatformID.Win32NT);
        yield return Form("platform other string", """{"platform":"SunOS"}""", p => p.Platform, PlatformID.Other);

        yield return Form("timeTaken number", """{"timeTaken":1234}""", p => p.TimeTaken, "1234");
        yield return Form("timeTaken fractional number", """{"timeTaken":1234.5}""", p => p.TimeTaken, "1234.5");
        yield return Form("timeTaken numeric string", """{"timeTaken":"1234"}""", p => p.TimeTaken, "1234");
        yield return Form("timeTaken null", """{"timeTaken":null}""", p => p.TimeTaken, null);

        yield return Form("builtAt epoch ms", """{"builtAt":1793577600000}""", p => p.BuiltAt, 1793577600000L);
        yield return Form("builtAt ISO-8601 Z", """{"builtAt":"2026-11-02T00:00:00.000Z"}""", p => p.BuiltAt, 1793577600000L);
        yield return Form("builtAt ISO-8601 offset", """{"builtAt":"2026-11-02T07:00:00.000+07:00"}""", p => p.BuiltAt, 1793577600000L);
        yield return Form("builtAt null", """{"builtAt":null}""", p => p.BuiltAt, null);
        yield return Form("timestamp epoch ms", """{"timestamp":1793577600001}""", p => p.Timestamp, 1793577600001L);
        yield return Form("timestamp ISO-8601", """{"timestamp":"2026-11-02T00:00:00.300Z"}""", p => p.Timestamp, 1793577600300L);

        yield return Form("errorMessage string", """{"errorMessage":"boom"}""", p => p.ErrorMessage, "boom");
        yield return Form("errorMessage array", """{"errorMessage":["first","second"]}""", p => p.ErrorMessage, "first\nsecond");
        yield return Form("errorMessage empty array", """{"errorMessage":[]}""", p => p.ErrorMessage, "");
        yield return Form("errorMessage null", """{"errorMessage":null}""", p => p.ErrorMessage, null);

        yield return Form("cpuSpeed number", """{"cpuSpeed":3000}""", p => p.CpuSpeed, new List<double> { 3000 });
        yield return Form("cpuSpeed array", """{"cpuSpeed":[3000,2400.5]}""", p => p.CpuSpeed, new List<double> { 3000, 2400.5 });
        yield return Form("cpuSpeed null", """{"cpuSpeed":null}""", p => p.CpuSpeed, null);
    }

    [TestCaseSource(nameof(DocumentedForms))]
    public void Converters_AcceptEveryDocumentedForm(string json, Func<Probe, object?> read, object? expected)
    {
        var probe = JsonSerializer.Deserialize<Probe>(json, WebOptions)!;

        read(probe).ShouldBe(expected);
    }

    [TestCase("platform")]
    [TestCase("timeTaken")]
    [TestCase("builtAt")]
    [TestCase("timestamp")]
    [TestCase("errorMessage")]
    [TestCase("cpuSpeed")]
    public void Converters_RejectAnObject(string field)
    {
        var json = $"{{\"{field}\":{{\"value\":1}}}}";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Probe>(json, WebOptions));
    }

    [Test]
    public void EpochOrIsoConverter_RejectsAStringThatIsNotATime()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<Probe>("""{"builtAt":"yesterday"}""", WebOptions));
    }

    [Test]
    public void StringOrStringArrayConverter_RejectsANonStringItem()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<Probe>("""{"errorMessage":["ok",1]}""", WebOptions));
    }

    [Test]
    public void NumberOrNumberArrayConverter_RejectsANonNumberItem()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<Probe>("""{"cpuSpeed":[3000,"fast"]}""", WebOptions));
    }
}
