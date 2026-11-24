using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

public class ModelSurfaceTests
{
    [Fact]
    public void OptionDescription_IsMultiValue_ForListsAndOptionals()
    {
        var list = new OptionDescription(
            "TriggerKeys", OptionType.Parse("List|Key"), "", null, null);
        var optional = new OptionDescription(
            "Maybe", OptionType.Parse("Optional|String"), "", null, null);
        var scalar = new OptionDescription(
            "Enabled", OptionType.Parse("Boolean"), "", null, null);

        Assert.True(list.IsMultiValue);
        Assert.True(optional.IsMultiValue);
        Assert.False(scalar.IsMultiValue);
    }

    [Fact]
    public void OptionDescription_DefaultsToEmptyConstraints()
    {
        var option = new OptionDescription("X", OptionType.Parse("Boolean"), "desc", null, null);

        Assert.NotNull(option.Constraints);
        Assert.Null(option.Constraints.Tooltip);
        Assert.Null(option.Tooltip);
    }

    [Fact]
    public void Constraints_ExposeAdditionalAnnotations()
    {
        var raw = new RawConfigValue();
        raw.SetChild("External", "fcitx5-configtool");
        raw.SetChild("LaunchSubConfig", "True");
        raw.SetChild("ListDisplayOption", "Name");
        raw.SetChild("IsEnum", "True");
        raw.SetChild("Font", "True");

        var constraints = new OptionConstraints(raw);

        Assert.Equal("fcitx5-configtool", constraints.ExternalUri);
        Assert.True(constraints.LaunchSubConfig);
        Assert.Equal("Name", constraints.ListDisplayOption);
        Assert.True(constraints.IsEnum);
        Assert.True(constraints.IsFont);
    }

    [Fact]
    public void ConfigDescription_LookupHelpers()
    {
        var option = new OptionDescription("A", OptionType.Parse("String"), "", null, null);
        var group = new ConfigTypeDescription("Group", new[] { option });
        var description = new ConfigDescription(new[] { group });

        Assert.Same(group, description.Root);
        Assert.Same(group, description.Find("Group"));
        Assert.Null(description.Find("Missing"));
        Assert.Same(option, description.FindOption("A"));
        Assert.Null(description.FindOption("B"));
    }

    [Fact]
    public void ConfigDescription_ResolveGroup_FollowsTypeName()
    {
        var reference = new OptionDescription(
            "Sub", OptionType.Parse("Sub$Inner"), "", null, null);
        var inner = new ConfigTypeDescription("Sub$Inner", Array.Empty<OptionDescription>());
        var description = new ConfigDescription(new[]
        {
            new ConfigTypeDescription("Root", new[] { reference }),
            inner,
        });

        Assert.Same(inner, description.ResolveGroup(reference));
    }

    [Fact]
    public void AddonInfo_MapsCategory()
    {
        var addon = new AddonInfo("pinyin", "Pinyin", "", 0, true, true);

        Assert.Equal(AddonCategory.InputMethod, addon.CategoryKind);
    }

    [Fact]
    public void AddonInfo_OutOfRangeCategory_IsUnknown()
    {
        Assert.Equal(AddonCategory.Unknown, new AddonInfo("x", "", "", 9, false, false).CategoryKind);
        Assert.Equal(AddonCategory.Unknown, new AddonInfo("x", "", "", -2, false, false).CategoryKind);
    }

    [Fact]
    public void EnumItem_FallsBackToNameWhenNoDisplayName()
    {
        var item = new EnumItem("0", "All", null);

        Assert.Equal("All", item.DisplayName);
        Assert.Equal("All", item.ToString());
    }

    [Fact]
    public void KeyboardLayoutVariant_EqualityUsesVariantId()
    {
        var a = new KeyboardLayoutVariant("intl", "International", null);
        var b = new KeyboardLayoutVariant("intl", "Different description", new[] { "en" });
        var c = new KeyboardLayoutVariant("dvorak", "Dvorak", null);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("intl (International)", a.ToString());
        Assert.Equal("dvorak", new KeyboardLayoutVariant("dvorak", "", null).ToString());
    }

    [Fact]
    public void KeyboardLayoutInfo_PreservesVariantOrder()
    {
        var layout = new KeyboardLayoutInfo(
            "us",
            "English (US)",
            new[] { "en" },
            new[]
            {
                new KeyboardLayoutVariant("intl", "", null),
                new KeyboardLayoutVariant("dvorak", "", null),
            });

        Assert.Equal(new[] { "intl", "dvorak" }, layout.Variants.Select(v => v.Variant));
    }
}
