using Agoda.DevExTelemetry.Core.Services;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.UnitTests;

[TestFixture]
public class StatusNormalizerTests
{
    private StatusNormalizer _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new StatusNormalizer();
    }

    [TestCase("Passed")]
    [TestCase("passed")]
    [TestCase("pass")]
    [TestCase("Succeeded")]
    public void Normalize_Passed_ReturnsPassed(string input)
    {
        _sut.Normalize(input).ShouldBe("Passed");
    }

    [TestCase("Failed")]
    [TestCase("failed")]
    [TestCase("fail")]
    public void Normalize_Failed_ReturnsFailed(string input)
    {
        _sut.Normalize(input).ShouldBe("Failed");
    }

    [TestCase("Skipped")]
    [TestCase("skipped")]
    [TestCase("skip")]
    [TestCase("Ignored")]
    [TestCase("ignored")]
    public void Normalize_Skipped_ReturnsSkipped(string input)
    {
        _sut.Normalize(input).ShouldBe("Skipped");
    }

    [TestCase("Pending")]
    [TestCase("pending")]
    [TestCase("todo")]
    public void Normalize_Pending_ReturnsPending(string input)
    {
        _sut.Normalize(input).ShouldBe("Pending");
    }

    [Test]
    public void Normalize_Null_ReturnsUnknown()
    {
        _sut.Normalize(null).ShouldBe("Unknown");
    }

    [Test]
    public void Normalize_Empty_ReturnsUnknown()
    {
        _sut.Normalize("").ShouldBe("Unknown");
    }

    [Test]
    public void Normalize_UnexpectedValue_ReturnsUnknown()
    {
        _sut.Normalize("SomethingElse").ShouldBe("Unknown");
    }
}
