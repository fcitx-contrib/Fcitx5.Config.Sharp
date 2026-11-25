namespace Fcitx5.Config.Core.Abstractions;

/// <summary>
/// Why fcitx5 (or the transport) refused a call.
/// </summary>
/// <remarks>
/// The set is deliberately small and not D-Bus flavoured, so a second transport can report into the
/// same vocabulary: the fcitx5 HTTP bridge's kinds collapse onto it — <c>not-implemented</c> is
/// <see cref="NotSupported"/>, <c>method-not-allowed</c> is an HTTP routing detail with no
/// counterpart, and <c>unavailable</c> is <see cref="FcitxUnavailableException"/> rather than a kind,
/// because "try again when the daemon is back" is a different decision from "the daemon said no".
/// </remarks>
public enum FcitxErrorKind
{
    /// <summary>The transport reported an error this library does not recognise.</summary>
    Unknown = 0,

    /// <summary>An argument was rejected: an unknown URI, a malformed name, a bad value.</summary>
    InvalidArgument,

    /// <summary>The named thing does not exist.</summary>
    NotFound,

    /// <summary>The daemon has no such method, or does not support the operation at all.</summary>
    NotSupported,

    /// <summary>The call conflicts with the current state, such as adding a group that exists.</summary>
    Conflict,

    /// <summary>The caller is not allowed to make the call.</summary>
    AccessDenied,

    /// <summary>The payload exceeds what the daemon accepts.</summary>
    PayloadTooLarge,

    /// <summary>The daemon refused the call for a reason it did not classify further.</summary>
    Failed,
}
