using System.Collections.Generic;
using System.Linq;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus;

namespace Fcitx5.Config.Core.Tests;

public class DbusConfigCodecTests
{
    private readonly DbusConfigCodec _codec = DbusConfigCodec.Instance;

    [Fact]
    public void DecodeValue_String_BecomesLeaf()
    {
        var value = _codec.DecodeValue("False");

        Assert.Equal("False", value.Value);
        Assert.True(value.IsLeaf);
    }

    [Fact]
    public void DecodeValue_Map_UsesEmptyKeyAsSelfValue()
    {
        var map = new Dictionary<string, object>
        {
            [""] = "self",
            ["Child"] = "leaf",
        };

        var value = _codec.DecodeValue(map);

        Assert.Equal("self", value.Value);
        Assert.Equal("leaf", value["Child"]!.Value);
    }

    [Fact]
    public void DecodeValue_NestedMaps_BecomeNestedNodes()
    {
        var map = new Dictionary<string, object>
        {
            ["Hotkey"] = new Dictionary<string, object>
            {
                ["TriggerKeys"] = new Dictionary<string, object>
                {
                    ["0"] = "Control+space",
                    ["1"] = "Zenkaku_Hankaku",
                },
            },
        };

        var value = _codec.DecodeValue(map);

        Assert.Equal(
            new[] { "Control+space", "Zenkaku_Hankaku" },
            value.FindPath("Hotkey/TriggerKeys")!.ToStringList());
    }

    [Fact]
    public void EncodeValue_Leaf_BecomesString()
    {
        var payload = _codec.EncodeValue(new RawConfigValue("True"));

        Assert.Equal("True", Assert.IsType<string>(payload));
    }

    [Fact]
    public void EncodeValue_NodeWithoutSelfValue_OmitsEmptyKey()
    {
        var node = new RawConfigValue();
        node.SetChild("A", "1");

        var entries = Assert.IsType<KeyValuePair<string, object>[]>(_codec.EncodeValue(node));

        Assert.DoesNotContain(entries, entry => entry.Key.Length == 0);
        Assert.Equal("1", Assert.Single(entries).Value);
    }

    [Fact]
    public void EncodeValue_NodeWithSelfValue_IncludesEmptyKey()
    {
        var node = new RawConfigValue("self");
        node.SetChild("A", "1");

        var entries = Assert.IsType<KeyValuePair<string, object>[]>(_codec.EncodeValue(node));

        Assert.Equal(RawConfigValue.SelfKey, entries[0].Key);
        Assert.Equal("self", entries[0].Value);
    }

    [Fact]
    public void EncodeValue_PreservesChildOrderOnTheWire()
    {
        var node = new RawConfigValue();
        node.SetChild("2", "c");
        node.SetChild("0", "a");
        node.SetChild("1", "b");

        var entries = Assert.IsType<KeyValuePair<string, object>[]>(_codec.EncodeValue(node));

        // fcitx5 builds its ordered map from wire order, so list order depends on this.
        Assert.Equal(new[] { "2", "0", "1" }, entries.Select(entry => entry.Key));
        Assert.Equal(new object[] { "c", "a", "b" }, entries.Select(entry => entry.Value));
    }

    [Fact]
    public void EncodeValue_NestedNodes_StayOrdered()
    {
        var node = new RawConfigValue();
        node.SetPath("Hotkey/TriggerKeys/1", "Zenkaku_Hankaku");
        node.SetPath("Hotkey/TriggerKeys/0", "Control+space");

        var root = Assert.IsType<KeyValuePair<string, object>[]>(_codec.EncodeValue(node));
        var hotkey = Assert.IsType<KeyValuePair<string, object>[]>(Assert.Single(root).Value);
        var triggerKeys = Assert.IsType<KeyValuePair<string, object>[]>(Assert.Single(hotkey).Value);

        Assert.Equal(new[] { "1", "0" }, triggerKeys.Select(entry => entry.Key));
        Assert.Equal(new object[] { "Zenkaku_Hankaku", "Control+space" }, triggerKeys.Select(entry => entry.Value));
    }

    [Theory]
    [InlineData("True")]
    [InlineData("")]
    public void RoundTrip_Leaf(string text)
    {
        var payload = _codec.EncodeValue(new RawConfigValue(text));
        var decoded = _codec.DecodeValue(payload);

        Assert.True(new RawConfigValue(text).DeepEquals(decoded));
    }

    [Fact]
    public void RoundTrip_List()
    {
        var original = RawConfigValue.FromList(new[] { "Control+space", "Zenkaku_Hankaku", "Hangul" });

        var decoded = _codec.DecodeValue(_codec.EncodeValue(original));

        Assert.True(original.DeepEquals(decoded));
        Assert.Equal(new[] { "Control+space", "Zenkaku_Hankaku", "Hangul" }, decoded.ToStringList());
    }

    [Fact]
    public void RoundTrip_NestedTree()
    {
        var original = new RawConfigValue();
        original.SetPath("Hotkey/TriggerKeys/0", "Control+space");
        original.SetPath("Behavior/ActiveByDefault", "False");

        var decoded = _codec.DecodeValue(_codec.EncodeValue(original));

        Assert.True(original.DeepEquals(decoded));
    }

    [Fact]
    public void DecodeDescription_MapsGroupsOptionsAndConstraints()
    {
        var types = new[]
        {
            new DbusConfigType
            {
                Name = "GlobalConfig",
                Options = new[]
                {
                    new DbusConfigOption
                    {
                        Name = "Hotkey",
                        Type = "Hotkey$HotkeyConfig",
                        Description = "快捷键",
                        DefaultValue = string.Empty,
                        Properties = new Dictionary<string, object>(),
                    },
                },
            },
            new DbusConfigType
            {
                Name = "Hotkey$HotkeyConfig",
                Options = new[]
                {
                    new DbusConfigOption
                    {
                        Name = "TriggerKeys",
                        Type = "List|Key",
                        Description = "切换输入法",
                        DefaultValue = new Dictionary<string, object>
                        {
                            ["0"] = "Control+space",
                            ["1"] = "Zenkaku_Hankaku",
                        },
                        Properties = new Dictionary<string, object>
                        {
                            ["ListConstrain"] = new Dictionary<string, object>
                            {
                                ["AllowModifierLess"] = "True",
                            },
                        },
                    },
                },
            },
        };

        var description = _codec.DecodeDescription(types);

        Assert.Equal(2, description.Types.Length);
        Assert.Equal("GlobalConfig", description.Root!.Name);

        var groupRef = description.FindOption("Hotkey")!;
        Assert.Equal(OptionKind.Group, groupRef.Type.Kind);
        Assert.Equal("Hotkey$HotkeyConfig", description.ResolveGroup(groupRef)!.Name);

        var triggerKeys = description.FindOption("TriggerKeys")!;
        Assert.True(triggerKeys.Type.IsList);
        Assert.Equal(OptionKind.Key, triggerKeys.Type.Kind);
        Assert.Equal(
            new[] { "Control+space", "Zenkaku_Hankaku" },
            triggerKeys.DefaultValue!.ToStringList());
        Assert.True(triggerKeys.Constraints.ListConstrain!.AllowModifierLess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-description")]
    public void DecodeDescription_InvalidPayload_IsEmpty(object? payload)
    {
        var description = _codec.DecodeDescription(payload);

        Assert.Empty(description.Types);
        Assert.Null(description.Root);
    }

    [Fact]
    public void DecodeDescription_EmptyArray_IsEmpty()
    {
        Assert.Empty(_codec.DecodeDescription(Array.Empty<DbusConfigType>()).Types);
    }

    [Fact]
    public void EncodeValue_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _codec.EncodeValue(null!));
    }
}
