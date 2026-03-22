using Agoda.DevExTelemetry.Core.Services;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.UnitTests;

[TestFixture]
public class EnvironmentDetectorTests
{
    private EnvironmentDetector _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new EnvironmentDetector();
    }

    [Test]
    public void Detect_DebuggerAttached_ReturnsLocal()
    {
        _sut.Detect("some-host", true, "Win32NT", null).ShouldBe("Local");
    }

    [Test]
    public void Detect_PlatformDocker_ReturnsCi()
    {
        _sut.Detect("some-host", false, "Docker", null).ShouldBe("CI");
    }

    [Test]
    public void Detect_PlatformAws_ReturnsCi()
    {
        _sut.Detect("some-host", false, "AWS", null).ShouldBe("CI");
    }

    [Test]
    public void Detect_NormalHostname_ReturnsLocal()
    {
        _sut.Detect("my-laptop", false, "Win32NT", null).ShouldBe("Local");
    }

    [TestCase("ci-runner-01")]
    [TestCase("build-agent-3")]
    [TestCase("agent-pool-1")]
    [TestCase("ci-server")]
    public void Detect_CiHostname_ReturnsCi(string hostname)
    {
        _sut.Detect(hostname, false, "Win32NT", null).ShouldBe("CI");
    }

    [Test]
    public void Detect_NullInputs_ReturnsLocal()
    {
        _sut.Detect(null, false, null, null).ShouldBe("Local");
    }

    [Test]
    public void Detect_DebuggerAttached_TakesPriority_OverCiPlatform()
    {
        _sut.Detect("ci-runner", true, "Docker", null).ShouldBe("Local");
    }
}
