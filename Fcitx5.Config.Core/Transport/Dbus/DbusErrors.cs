using System;
using Fcitx5.Config.Core.Abstractions;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Turns the D-Bus failures into the transport neutral ones, and is the only place that knows
/// anything about them.
/// </summary>
/// <remarks>
/// <para>
/// The last case matches the base of every exception the D-Bus library declares, so a type added in
/// a later version cannot leak out uncharged; anything that is not one of its types — a caller's
/// argument error, cancellation, an already neutral exception — is left for the caller to rethrow.
/// </para>
/// <para>
/// The original is kept as <see cref="Exception.InnerException"/> for logs and debuggers. It belongs
/// to the transport, so it is not part of the contract a caller reads.
/// </para>
/// </remarks>
internal static class DbusErrors
{
    private const string ErrorPrefix = "org.freedesktop.DBus.Error.";

    /// <summary>
    /// Returns the neutral exception to throw instead, or <see langword="null"/> when the failure is
    /// not the transport's.
    /// </summary>
    public static Exception? Translate(Exception exception) => exception switch
    {
        DBusErrorReplyException reply => FromReply(reply),

        // The bus is gone, or the owner it reported changed under us: no reply is coming, so the
        // call has to be made again rather than interpreted.
        DBusConnectionException or DBusOwnerChangedException =>
            new FcitxUnavailableException(exception.Message, null, exception),

        // A reply or a payload this library could not read.
        DBusExceptionBase => new FcitxUnexpectedResponseException(exception.Message, exception),

        _ => null,
    };

    private static Exception FromReply(DBusErrorReplyException reply)
    {
        var name = StripPrefix(reply.ErrorName);
        var message = string.IsNullOrEmpty(reply.ErrorMessage) ? reply.ErrorName : reply.ErrorMessage;

        // An error reply can itself mean "there is nobody there". Folding it into the unavailable
        // type leaves one representation per situation, so a caller does not switch on a kind to
        // decide whether waiting makes sense.
        return IsUnavailable(name)
            ? new FcitxUnavailableException(message, reply.ErrorName, reply)
            : new FcitxErrorException(KindOf(name), message, reply.ErrorName, reply);
    }

    /// <summary>Drops the standard namespace, so the tables below read as plain names.</summary>
    private static string StripPrefix(string errorName) =>
        errorName.StartsWith(ErrorPrefix, StringComparison.Ordinal)
            ? errorName[ErrorPrefix.Length..]
            : errorName;

    private static bool IsUnavailable(string name) =>
        name is "ServiceUnknown" or "NameHasNoOwner" or "Disconnected" or "NoServer" or "Timeout" or "TimedOut";

    /// <summary>
    /// Maps a D-Bus error name onto the shared vocabulary.
    /// </summary>
    /// <remarks>
    /// An unrecognised name becomes <see cref="FcitxErrorKind.Unknown"/> instead of a guess, and
    /// <see cref="FcitxException.TransportErrorName"/> keeps it for the caller to inspect.
    /// </remarks>
    private static FcitxErrorKind KindOf(string name) => name switch
    {
        "UnknownMethod" or "UnknownInterface" or "UnknownObject" or "UnknownProperty"
            or "NotSupported" or "PropertyReadOnly" => FcitxErrorKind.NotSupported,

        "InvalidArgs" or "InvalidSignature" or "InvalidFileContent" => FcitxErrorKind.InvalidArgument,

        "FileNotFound" => FcitxErrorKind.NotFound,
        "AccessDenied" or "AuthFailed" => FcitxErrorKind.AccessDenied,
        "LimitsExceeded" => FcitxErrorKind.PayloadTooLarge,
        "Failed" or "NoMemory" or "NoReply" or "IOError" => FcitxErrorKind.Failed,
        _ => FcitxErrorKind.Unknown,
    };
}
