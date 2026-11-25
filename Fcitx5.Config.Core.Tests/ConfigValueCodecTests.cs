using System;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus;
using Tmds.DBus.Protocol;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// Covers the conversion between <see cref="RawConfigValue"/> and <see cref="VariantValue"/>.
/// </summary>
/// <remarks>
/// These are properties of the conversion itself, so they are checked without a bus. The framing
/// around them is covered by <see cref="DbusSessionTests"/> against the fake daemon.
/// </remarks>
public class ConfigValueCodecTests
{
    [Fact]
    public void WriteThenRead_RoundTripsValuesAndChildOrder()
    {
        var node = new RawConfigValue("root");
        node.SetChild("1", "second");
        node.SetChild("0", "first");

        // DeepEquals compares the own value, the child names, their order and their contents.
        Assert.True(node.DeepEquals(ConfigValueCodec.Read(ConfigValueCodec.Write(node))));
    }

    [Fact]
    public void Read_RejectsALeafThatIsNotAString()
    {
        var exception = Assert.Throws<FcitxUnexpectedResponseException>(
            () => ConfigValueCodec.Read(VariantValue.Int32(1)));

        Assert.Contains("Int32", exception.Message, StringComparison.Ordinal);
    }
}
