using System.ComponentModel;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// The <c>org.fcitx.Fcitx.Controller1</c> interface. Method names follow Tmds.DBus conventions: the
/// proxy strips the <c>Async</c> suffix to derive the D-Bus method name.
/// </summary>
/// <remarks>
/// Signatures were verified against a running fcitx5 via
/// <c>org.freedesktop.DBus.Introspectable.Introspect</c>. Only the members the library consumes are
/// modelled; the daemon exposes more.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[DBusInterface("org.fcitx.Fcitx.Controller1")]
public interface IFcitxController : IDBusObject
{
    Task<(object Value, DbusConfigType[] Description)> GetConfigAsync(string uri);

    Task SetConfigAsync(string uri, object value);

    Task<DbusAddonInfo[]> GetAddonsAsync();

    Task<DbusAddonInfoV2[]> GetAddonsV2Async();

    Task SetAddonsStateAsync(DbusAddonState[] states);

    Task<DbusInputMethodEntry[]> AvailableInputMethodsAsync();

    Task<DbusLayoutInfo[]> AvailableKeyboardLayoutsAsync();

    Task<string[]> InputMethodGroupsAsync();

    Task<DbusFullInputMethodGroupInfo> FullInputMethodGroupInfoAsync(string groupName);

    Task SetInputMethodGroupInfoAsync(string groupName, string defaultLayout, DbusStringPair[] entries);

    Task AddInputMethodGroupAsync(string groupName);

    Task RemoveInputMethodGroupAsync(string groupName);

    Task SwitchInputMethodGroupAsync(string groupName);

    Task<string> CurrentInputMethodGroupAsync();

    Task<int> StateAsync();

    Task<string> CurrentInputMethodAsync();

    Task SetCurrentIMAsync(string uniqueName);

    Task<bool> CanRestartAsync();

    Task RestartAsync();

    Task ExitAsync();

    Task RefreshAsync();

    Task ReloadConfigAsync();

    Task ReloadAddonConfigAsync(string addonName);

    Task ActivateAsync();

    Task DeactivateAsync();

    Task ToggleAsync();
}
