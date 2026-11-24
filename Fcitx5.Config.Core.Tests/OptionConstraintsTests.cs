using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

public class OptionConstraintsTests
{
    [Fact]
    public void IntegerBounds_AreParsed()
    {
        var raw = new RawConfigValue();
        raw.SetChild("IntMin", "-1");
        raw.SetChild("IntMax", "5000");

        var constraints = new OptionConstraints(raw);

        Assert.Equal(-1, constraints.IntMin);
        Assert.Equal(5000, constraints.IntMax);
    }

    [Fact]
    public void InvalidIntegerBounds_AreIgnored()
    {
        var raw = new RawConfigValue();
        raw.SetChild("IntMin", "not-a-number");

        Assert.Null(new OptionConstraints(raw).IntMin);
    }

    [Fact]
    public void Enum_And_EnumI18n_ArePaired()
    {
        var raw = new RawConfigValue();
        var names = raw.GetOrAddChild("Enum");
        names.SetChild("0", "All");
        names.SetChild("1", "Program");
        names.SetChild("2", "No");

        var localized = raw.GetOrAddChild("EnumI18n");
        localized.SetChild("0", "所有");
        localized.SetChild("1", "程序");
        localized.SetChild("2", "否");

        var items = new OptionConstraints(raw).EnumItems();

        Assert.Equal(3, items.Count);
        Assert.Equal("All", items[0].Name);
        Assert.Equal("所有", items[0].DisplayName);
        Assert.Equal("No", items[2].Name);
        Assert.Equal("否", items[2].DisplayName);
    }

    [Fact]
    public void Enum_WithoutLocalization_FallsBackToNames()
    {
        var raw = new RawConfigValue();
        var names = raw.GetOrAddChild("Enum");
        names.SetChild("0", "On");
        names.SetChild("1", "Off");

        var items = new OptionConstraints(raw).EnumItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("On", items[0].DisplayName);
    }

    [Fact]
    public void BooleanFlags_AreRecognised()
    {
        var raw = new RawConfigValue();
        raw.SetChild("AllowModifierOnly", "True");
        raw.SetChild("AllowModifierLess", "False");
        raw.SetChild("Font", "True");
        raw.SetChild("IsRegex", "True");

        var constraints = new OptionConstraints(raw);

        Assert.True(constraints.AllowModifierOnly);
        Assert.False(constraints.AllowModifierLess);
        Assert.True(constraints.IsFont);
        Assert.True(constraints.IsRegex);
    }

    [Fact]
    public void NestedListConstrain_IsExposed()
    {
        var raw = new RawConfigValue();
        var listConstrain = raw.GetOrAddChild("ListConstrain");
        listConstrain.SetChild("AllowModifierLess", "True");
        raw.SetChild("Tooltip", "tip");

        var constraints = new OptionConstraints(raw);

        Assert.Equal("tip", constraints.Tooltip);
        Assert.NotNull(constraints.ListConstrain);
        Assert.True(constraints.ListConstrain!.AllowModifierLess);
    }

    [Fact]
    public void ExternalUri_And_LaunchSubConfig_AreExposed()
    {
        var raw = new RawConfigValue();
        raw.SetChild("External", "fcitx5-configtool");
        raw.SetChild("LaunchSubConfig", "True");

        var constraints = new OptionConstraints(raw);

        Assert.Equal("fcitx5-configtool", constraints.ExternalUri);
        Assert.True(constraints.LaunchSubConfig);
    }

    [Fact]
    public void EnumItem_EqualityUsesStoredNameOnly()
    {
        var a = new EnumItem("0", "All", "所有");
        var b = new EnumItem("2", "All", null);
        var c = new EnumItem("0", "No", "否");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void EnumItems_AreComputedOnce()
    {
        var raw = new RawConfigValue();
        var names = raw.GetOrAddChild("Enum");
        names.SetChild("0", "On");
        var constraints = new OptionConstraints(raw);

        var first = constraints.EnumItems();
        var second = constraints.EnumItems();

        Assert.Same(first, second);
    }

    [Fact]
    public void Empty_IsNotShared()
    {
        var first = OptionConstraints.Empty;
        first.Raw.SetChild("Tooltip", "mutated");

        Assert.Null(OptionConstraints.Empty.Tooltip);
        Assert.NotSame(OptionConstraints.Empty, OptionConstraints.Empty);
    }

    [Fact]
    public void Constructor_ClonesInput()
    {
        var raw = new RawConfigValue();
        raw.SetChild("Tooltip", "original");

        var constraints = new OptionConstraints(raw);
        raw.SetChild("Tooltip", "mutated");

        Assert.Equal("original", constraints.Tooltip);
    }
}
