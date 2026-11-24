using Fcitx5.Config.Core.Fcitx;

namespace Fcitx5.Config.Core.Tests;

public class FcitxConfigUriTests
{
    [Fact]
    public void Global_UsesCanonicalUri()
    {
        Assert.Equal("fcitx://config/global", FcitxConfigUri.Global());
    }

    [Fact]
    public void Addon_BuildsUri()
    {
        Assert.Equal("fcitx://config/addon/pinyin", FcitxConfigUri.Addon("pinyin"));
    }

    [Fact]
    public void Addon_WithSubPath_BuildsUri()
    {
        Assert.Equal("fcitx://config/addon/pinyin/SomeSub", FcitxConfigUri.Addon("pinyin", "SomeSub"));
    }

    [Fact]
    public void InputMethod_BuildsUri()
    {
        Assert.Equal("fcitx://config/inputmethod/pinyin", FcitxConfigUri.InputMethod("pinyin"));
    }

    [Theory]
    [InlineData("")]
    public void Builders_RejectEmptyNames(string name)
    {
        Assert.Throws<ArgumentException>(() => FcitxConfigUri.Addon(name));
        Assert.Throws<ArgumentException>(() => FcitxConfigUri.InputMethod(name));
    }

    [Fact]
    public void TryParse_Global()
    {
        Assert.True(FcitxConfigUri.TryParse("fcitx://config/global", out var parsed));

        Assert.Equal(FcitxConfigTarget.Global, parsed.Target);
        Assert.Equal(string.Empty, parsed.Name);
        Assert.Null(parsed.SubPath);
    }

    [Fact]
    public void TryParse_Addon()
    {
        Assert.True(FcitxConfigUri.TryParse("fcitx://config/addon/pinyin", out var parsed));

        Assert.Equal(FcitxConfigTarget.Addon, parsed.Target);
        Assert.Equal("pinyin", parsed.Name);
        Assert.Null(parsed.SubPath);
    }

    [Fact]
    public void TryParse_AddonWithSubPath()
    {
        Assert.True(FcitxConfigUri.TryParse("fcitx://config/addon/pinyin/a/b", out var parsed));

        Assert.Equal(FcitxConfigTarget.Addon, parsed.Target);
        Assert.Equal("pinyin", parsed.Name);
        Assert.Equal("a/b", parsed.SubPath);
    }

    [Fact]
    public void TryParse_InputMethod()
    {
        Assert.True(FcitxConfigUri.TryParse("fcitx://config/inputmethod/rime", out var parsed));

        Assert.Equal(FcitxConfigTarget.InputMethod, parsed.Target);
        Assert.Equal("rime", parsed.Name);
    }

    [Fact]
    public void TryParse_AddonWithTrailingSlash_HasEmptySubPath()
    {
        Assert.True(FcitxConfigUri.TryParse("fcitx://config/addon/pinyin/", out var parsed));

        Assert.Equal(FcitxConfigTarget.Addon, parsed.Target);
        Assert.Equal("pinyin", parsed.Name);
        Assert.Equal(string.Empty, parsed.SubPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fcitx://nonsense")]
    [InlineData("fcitx://config/")]
    [InlineData("fcitx://config/addon/")]
    [InlineData("fcitx://config/inputmethod/")]
    [InlineData("fcitx://config/globalx")]
    [InlineData("http://example.com")]
    public void TryParse_RejectsInvalid(string? uri)
    {
        Assert.False(FcitxConfigUri.TryParse(uri, out var parsed));
        Assert.Equal(FcitxConfigTarget.Unknown, parsed.Target);
    }
}
