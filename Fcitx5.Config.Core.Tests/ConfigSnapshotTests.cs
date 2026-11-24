using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

public class ConfigSnapshotTests
{
    private static ConfigSnapshot CreateSnapshot()
    {
        var value = new RawConfigValue();
        value.SetPath("Behavior/ActiveByDefault", "True");

        var option = new OptionDescription(
            "PageSize",
            OptionType.Parse("Integer"),
            "Page size",
            new RawConfigValue("5"),
            OptionConstraints.Empty);

        var description = new ConfigDescription(
            new[] { new ConfigTypeDescription("Behavior$BehaviorConfig", new[] { option }) });

        return new ConfigSnapshot("fcitx://config/global", value, description);
    }

    [Fact]
    public void GetOptionValue_ReturnsCurrentValue()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal("True", snapshot.GetOptionValue("Behavior/ActiveByDefault")!.Value);
    }

    [Fact]
    public void GetOptionValue_FallsBackToDefault()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal("5", snapshot.GetOptionValue("PageSize")!.Value);
    }

    [Fact]
    public void GetOptionValue_ReturnsNullWhenUnknown()
    {
        var snapshot = CreateSnapshot();

        Assert.Null(snapshot.GetOptionValue("Missing"));
    }

    [Fact]
    public void FindOption_SearchesAllGroups()
    {
        var snapshot = CreateSnapshot();

        Assert.NotNull(snapshot.FindOption("PageSize"));
        Assert.Null(snapshot.FindOption("Nope"));
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ConfigSnapshot(null!, new RawConfigValue(), ConfigDescription.Empty));
        Assert.Throws<ArgumentNullException>(
            () => new ConfigSnapshot("uri", null!, ConfigDescription.Empty));
    }
}
