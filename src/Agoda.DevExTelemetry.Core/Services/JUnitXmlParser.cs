using System.Xml.Linq;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IJUnitXmlParser
{
    IEnumerable<TestCase> Parse(Stream xmlStream, string testRunId);
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

    public IEnumerable<TestCase> Parse(Stream xmlStream, string testRunId)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JUnit XML");
            yield break;
        }

        var testCaseElements = doc.Descendants("testcase");

        foreach (var element in testCaseElements)
        {
            TestCase? testCase;
            try
            {
                testCase = ParseTestCase(element, testRunId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed testcase element");
                continue;
            }

            if (testCase != null)
                yield return testCase;
        }
    }

    private static TestCase ParseTestCase(XElement element, string testRunId)
    {
        var name = element.Attribute("name")?.Value ?? string.Empty;
        var className = element.Attribute("classname")?.Value;
        var timeStr = element.Attribute("time")?.Value;

        double? durationMs = null;
        if (double.TryParse(timeStr, out var timeSeconds))
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
