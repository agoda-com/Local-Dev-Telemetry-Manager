using Agoda.DevExTelemetry.Core.Services;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.UnitTests;

[TestFixture]
public class BuildCategoryClassifierTests
{
    private BuildCategoryClassifier _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new BuildCategoryClassifier();
    }

    [Test]
    public void Classify_DotNet_ReturnsApiCategory()
    {
        var (category, reloadType) = _sut.Classify(".Net", null);
        category.ShouldBe("API");
        reloadType.ShouldBeNull();
    }

    [Test]
    public void Classify_AspNetStartup_ReturnsApiCategory()
    {
        var (category, reloadType) = _sut.Classify(".AspNetStartup", null);
        category.ShouldBe("API");
        reloadType.ShouldBeNull();
    }

    [Test]
    public void Classify_GradleTalaiot_ReturnsApiCategory()
    {
        var (category, reloadType) = _sut.Classify("GradleTalaiot", null);
        category.ShouldBe("API");
        reloadType.ShouldBeNull();
    }

    [Test]
    public void Classify_Webpack_ReturnsClientsideCategory_FullReload()
    {
        var (category, reloadType) = _sut.Classify("webpack", null);
        category.ShouldBe("Clientside");
        reloadType.ShouldBe("full");
    }

    [Test]
    public void Classify_Vite_ReturnsClientsideCategory_FullReload()
    {
        var (category, reloadType) = _sut.Classify("vite", null);
        category.ShouldBe("Clientside");
        reloadType.ShouldBe("full");
    }

    [Test]
    public void Classify_ViteHmr_ReturnsClientsideCategory_HotReload()
    {
        var (category, reloadType) = _sut.Classify("vitehmr", null);
        category.ShouldBe("Clientside");
        reloadType.ShouldBe("hot");
    }

    [Test]
    public void Classify_WebpackHmr_ReturnsClientsideCategory_HotReload()
    {
        var (category, reloadType) = _sut.Classify("webpack", "hmr");
        category.ShouldBe("Clientside");
        reloadType.ShouldBe("hot");
    }

    [Test]
    public void Classify_ApiCategory_ReturnsNullReloadType()
    {
        var (category, reloadType) = _sut.Classify(".Net", null);
        category.ShouldBe("API");
        reloadType.ShouldBeNull();
    }

    [Test]
    public void Classify_AspNetResponse_ReturnsApiCategory()
    {
        var (category, reloadType) = _sut.Classify(".AspNetResponse", null);
        category.ShouldBe("API");
        reloadType.ShouldBeNull();
    }
}
