using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

public class OptionTypeTests
{
    [Theory]
    [InlineData("Boolean", OptionKind.Boolean)]
    [InlineData("Integer", OptionKind.Integer)]
    [InlineData("String", OptionKind.String)]
    [InlineData("Key", OptionKind.Key)]
    [InlineData("Color", OptionKind.Color)]
    [InlineData("Version", OptionKind.Version)]
    [InlineData("I18NString", OptionKind.I18NString)]
    [InlineData("Enum", OptionKind.Enum)]
    [InlineData("External", OptionKind.External)]
    [InlineData("SubConfig", OptionKind.SubConfig)]
    public void Parse_KnownScalarTypes(string raw, OptionKind expected)
    {
        var type = OptionType.Parse(raw);

        Assert.Equal(expected, type.Kind);
        Assert.False(type.IsList);
        Assert.False(type.IsOptional);
        Assert.Null(type.GroupName);
    }

    [Fact]
    public void Parse_ListOfKeys_ExposesElementType()
    {
        var type = OptionType.Parse("List|Key");

        Assert.True(type.IsList);
        Assert.Equal(OptionKind.Key, type.Kind);
        Assert.Equal(OptionKind.Key, type.ElementType!.Kind);
        Assert.Equal("List|Key", type.Raw);
    }

    [Fact]
    public void Parse_OptionalString_ExposesElementType()
    {
        var type = OptionType.Parse("Optional|String");

        Assert.True(type.IsOptional);
        Assert.False(type.IsList);
        Assert.Equal(OptionKind.String, type.Kind);
        Assert.NotNull(type.ElementType);
    }

    [Fact]
    public void Parse_GroupReference_UsesRawNameAsGroupName()
    {
        var type = OptionType.Parse("Hotkey$HotkeyConfig");

        Assert.Equal(OptionKind.Group, type.Kind);
        Assert.Equal("Hotkey$HotkeyConfig", type.GroupName);
    }

    [Fact]
    public void Parse_ListOfGroups_KeepsGroupName()
    {
        var type = OptionType.Parse("List|Entries$EntryConfig");

        Assert.True(type.IsList);
        Assert.Equal(OptionKind.Group, type.Kind);
        Assert.Equal("Entries$EntryConfig", type.GroupName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_Empty_IsUnknown(string? raw)
    {
        var type = OptionType.Parse(raw);

        Assert.Equal(OptionKind.Unknown, type.Kind);
    }

    [Fact]
    public void Parse_UnknownScalarName_TreatedAsGroup()
    {
        var type = OptionType.Parse("SomeFutureType");

        Assert.Equal(OptionKind.Group, type.Kind);
        Assert.Equal("SomeFutureType", type.GroupName);
    }
}
