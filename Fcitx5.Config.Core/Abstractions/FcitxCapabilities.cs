using System;

namespace Fcitx5.Config.Core.Abstractions;

/// <summary>
/// The features a transport provides. Transports differ substantially: the D-Bus transport
/// exposes the full controller API, while the HTTP (Beast) transport used on macOS and Windows
/// currently only supports configuration read/write plus a few runtime commands.
/// </summary>
[Flags]
public enum FcitxCapabilities
{
    /// <summary>No features.</summary>
    None = 0,

    /// <summary>Configuration can be read (<c>GetConfig</c> / <c>GET /config/...</c>).</summary>
    ConfigRead = 1 << 0,

    /// <summary>Configuration can be written (<c>SetConfig</c> / <c>POST /config/...</c>).</summary>
    ConfigWrite = 1 << 1,

    /// <summary>The addon list can be enumerated.</summary>
    AddonEnumeration = 1 << 2,

    /// <summary>Addons can be enabled or disabled.</summary>
    AddonControl = 1 << 3,

    /// <summary>Available input methods can be enumerated.</summary>
    InputMethodEnumeration = 1 << 4,

    /// <summary>Input method groups can be enumerated and edited.</summary>
    InputMethodGroupManagement = 1 << 5,

    /// <summary>The runtime state and the current input method can be read and changed.</summary>
    RuntimeCommands = 1 << 6,

    /// <summary>The daemon can be reloaded, restarted or exited.</summary>
    Lifecycle = 1 << 7,

    /// <summary>Everything the library supports.</summary>
    Full = ConfigRead | ConfigWrite | AddonEnumeration | AddonControl |
           InputMethodEnumeration | InputMethodGroupManagement | RuntimeCommands | Lifecycle,
}
