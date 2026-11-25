using System;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Transport.Dbus;
using Tmds.DBus.Protocol;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// Covers the mapping of D-Bus failures onto the neutral exceptions: one case per rule, each built
/// from the exception the bus or the daemon raises. <see cref="DbusSessionTests"/> checks that the
/// mapping is reached through a real bus.
/// </summary>
public class DbusErrorsTests
{
    [Fact]
    public void AnUnknownMethod_IsNotSupported()
    {
        var original = new DBusErrorReplyException(
            "org.freedesktop.DBus.Error.UnknownMethod",
            "no such method");

        var error = Assert.IsType<FcitxErrorException>(Translate(original));

        Assert.Equal(FcitxErrorKind.NotSupported, error.Kind);
        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod", error.TransportErrorName);
        Assert.Equal("no such method", error.Message);
        Assert.Same(original, error.InnerException);
    }

    [Theory]
    [InlineData("org.freedesktop.DBus.Error.InvalidArgs", FcitxErrorKind.InvalidArgument)]
    [InlineData("org.freedesktop.DBus.Error.FileNotFound", FcitxErrorKind.NotFound)]
    [InlineData("org.freedesktop.DBus.Error.AccessDenied", FcitxErrorKind.AccessDenied)]
    [InlineData("org.freedesktop.DBus.Error.LimitsExceeded", FcitxErrorKind.PayloadTooLarge)]
    [InlineData("org.freedesktop.DBus.Error.Failed", FcitxErrorKind.Failed)]
    public void AnErrorReply_MapsToItsKind(string errorName, FcitxErrorKind expected)
    {
        var error = Assert.IsType<FcitxErrorException>(
            Translate(new DBusErrorReplyException(errorName, "refused")));

        Assert.Equal(expected, error.Kind);
        Assert.Equal(errorName, error.TransportErrorName);
    }

    [Fact]
    public void AnUnmappedErrorName_KeepsTheNameAndReportsUnknown()
    {
        var error = Assert.IsType<FcitxErrorException>(
            Translate(new DBusErrorReplyException("com.example.Error.Nope", "?")));

        Assert.Equal(FcitxErrorKind.Unknown, error.Kind);
        Assert.Equal("com.example.Error.Nope", error.TransportErrorName);
    }

    [Theory]
    [InlineData("org.freedesktop.DBus.Error.ServiceUnknown")]
    [InlineData("org.freedesktop.DBus.Error.NameHasNoOwner")]
    [InlineData("org.freedesktop.DBus.Error.Disconnected")]
    public void AReplyAboutAMissingPeer_IsUnavailable(string errorName)
    {
        var unavailable = Assert.IsType<FcitxUnavailableException>(
            Translate(new DBusErrorReplyException(errorName, "")));

        Assert.Equal(errorName, unavailable.TransportErrorName);

        // The daemon sent no text, so the name is all there is to show.
        Assert.Equal(errorName, unavailable.Message);
    }

    [Fact]
    public void AConnectionThatIsGone_IsUnavailable()
    {
        Assert.Equal(
            "refused",
            Assert.IsType<FcitxUnavailableException>(Translate(new DBusConnectFailedException("refused"))).Message);

        Assert.Equal(
            "closed",
            Assert.IsType<FcitxUnavailableException>(Translate(new DBusConnectionClosedException("closed"))).Message);
    }

    [Fact]
    public void AReplyThisLibraryCannotRead_IsAnUnexpectedResponse()
    {
        // DBusReadException is deliberately not named by the mapping: reaching it proves the last
        // case matches the library's base type, so a type added in a later version cannot leak.
        var original = new DBusReadException("a leaf must be a string");

        var unexpected = Assert.IsType<FcitxUnexpectedResponseException>(Translate(original));

        Assert.Same(original, unexpected.InnerException);
    }

    [Fact]
    public void AFailureThatIsNotTheTransports_IsLeftAlone()
    {
        Assert.Null(DbusErrors.Translate(new InvalidOperationException("a caller mistake")));

        // Argument errors and cancellation are the caller's business and keep their own types.
        Assert.Null(DbusErrors.Translate(new ArgumentException("bad uri")));
        Assert.Null(DbusErrors.Translate(new OperationCanceledException()));
    }

    private static Exception Translate(Exception exception) =>
        DbusErrors.Translate(exception)
        ?? throw new InvalidOperationException($"Expected a translation for {exception.GetType().Name}.");
}
