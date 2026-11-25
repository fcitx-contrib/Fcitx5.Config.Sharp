using System;

namespace Fcitx5.Config.Core.Abstractions;

/// <summary>
/// A failure that came from fcitx5 or from the transport underneath.
/// </summary>
/// <remarks>
/// Argument errors stay the usual <see cref="ArgumentException"/> family, a feature the transport
/// does not advertise throws <see cref="NotSupportedException"/> before any call is made, and
/// cancellation stays <see cref="OperationCanceledException"/>. Catching this type therefore covers
/// everything a transport can fail with, on any transport; each subclass says what to do about it.
/// </remarks>
public abstract class FcitxException : Exception
{
    private protected FcitxException(
        string message,
        string? transportErrorName,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TransportErrorName = transportErrorName;
    }

    /// <summary>
    /// The error name the transport itself reported, when it had one.
    /// </summary>
    /// <remarks>
    /// It is for logging and for bug reports, never for control flow: use
    /// <see cref="FcitxErrorException.Kind"/> for that. The value belongs to the transport, so a
    /// D-Bus error name here says nothing about what another transport would report for the same
    /// situation.
    /// </remarks>
    public string? TransportErrorName { get; }
}

/// <summary>
/// fcitx5 cannot be reached: no bus, no daemon, or the connection went away.
/// </summary>
/// <remarks>
/// The call did not happen, or its outcome is unknown; either way retrying is safe once the daemon
/// is back. <see cref="IFcitxSession.IsAvailable"/> and
/// <see cref="IFcitxSession.AvailabilityChanged"/> tell when that is, so a UI can wait instead of
/// polling.
/// </remarks>
public sealed class FcitxUnavailableException : FcitxException
{
    internal FcitxUnavailableException(
        string message,
        string? transportErrorName = null,
        Exception? innerException = null)
        : base(message, transportErrorName, innerException)
    {
    }
}

/// <summary>
/// fcitx5 refused a call.
/// </summary>
/// <remarks>
/// <see cref="Kind"/> says why, in a vocabulary every transport shares.
/// </remarks>
public sealed class FcitxErrorException : FcitxException
{
    internal FcitxErrorException(
        FcitxErrorKind kind,
        string message,
        string? transportErrorName = null,
        Exception? innerException = null)
        : base(message, transportErrorName, innerException)
    {
        Kind = kind;
    }

    /// <summary>Why the call was refused.</summary>
    public FcitxErrorKind Kind { get; }
}

/// <summary>
/// A reply could not be read, which means the daemon and this library disagree about the protocol.
/// </summary>
/// <remarks>
/// This is not a caller mistake and retrying will not help: it points at a fcitx5 version this
/// library does not understand, or at a defect on one side. The message names what was expected and
/// what arrived.
/// </remarks>
public sealed class FcitxUnexpectedResponseException : FcitxException
{
    internal FcitxUnexpectedResponseException(string message, Exception? innerException = null)
        : base(message, null, innerException)
    {
    }
}
