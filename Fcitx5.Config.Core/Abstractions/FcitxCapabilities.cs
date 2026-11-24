using System;

namespace Fcitx5.Config.Core.Abstractions;

/// <summary>
/// The features a transport provides.
/// </summary>
/// <remarks>
/// The D-Bus transport implements all of them. The flags exist because a transport need not:
/// a future one may be able to read and write configuration without, say, enumerating groups, and
/// a caller is expected to check for the flag it needs instead of assuming the whole set.
/// </remarks>
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
