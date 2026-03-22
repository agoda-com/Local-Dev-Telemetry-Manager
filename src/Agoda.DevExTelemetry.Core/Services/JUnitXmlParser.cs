using System.Globalization;
using System.Xml.Linq;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.Core.Services;

public class JUnitXmlParseResult
{
    public List<TestCase> TestCases { get; init; } = new();
    public bool HasParseErrors { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IJUnitXmlParser
{
    JUnitXmlParseResult Parse(Stream xmlStream, string testRunId);
}

[RegisterPerRequest]
public class JUnitXmlParser : IJUnitXmlParser
{
    private const int MaxErrorMessageLength = 4096;
    private readonly ILogger<JUnitXmlParser> _logger;

    public JUnitXmlParser(ILogger<JUnitXmlParser> logger)
    {
        _logger = logger;
    }

    public JUnitXmlParseResult Parse(Stream xmlStream, string testRunId)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JUnit XML");
            return new JUnitXmlParseResult
            {
                HasParseErrors = true,
                ErrorMessage = $"Invalid XML: {ex.Message}"
            };
        }

        var testCases = new List<TestCase>();
        var testCaseElements = doc.Descendants("testcase");

        foreach (var element in testCaseElements)
        {
            try
            {
                var testCase = ParseTestCase(element, testRunId);
                if (testCase != null)
                    testCases.Add(testCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed testcase element");
            }
        }

        return new JUnitXmlParseResult { TestCases = testCases };
    }

    private static TestCase ParseTestCase(XElement element, string testRunId)
    {
        var name = element.Attribute("name")?.Value ?? string.Empty;
        var className = element.Attribute("classname")?.Value;
        var timeStr = element.Attribute("time")?.Value;

        double? durationMs = null;
        if (double.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeSeconds)
            && double.IsFinite(timeSeconds) && timeSeconds >= 0)
            durationMs = timeSeconds * 1000;

        var status = DetermineStatus(element);
        var errorMessage = ExtractErrorMessage(element);

        return new TestCase
        {
            TestRunId = testRunId,
            Name = name,
            FullName = className != null ? $"{className}.{name}" : name,
            ClassName = className,
            Status = status,
            DurationMs = durationMs,
            ErrorMessage = errorMessage != null
                ? Truncate(errorMessage, MaxErrorMessageLength)
                : null
        };
    }

    private static string DetermineStatus(XElement element)
    {
        if (element.Element("failure") != null)
            return "Failed";
        if (element.Element("error") != null)
            return "Failed";
        if (element.Element("skipped") != null)
            return "Skipped";
        return "Passed";
    }

    private static string? ExtractErrorMessage(XElement element)
    {
        var failure = element.Element("failure");
        if (failure != null)
            return failure.Attribute("message")?.Value ?? failure.Value;

        var error = element.Element("error");
        if (error != null)
            return error.Attribute("message")?.Value ?? error.Value;

        return null;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
