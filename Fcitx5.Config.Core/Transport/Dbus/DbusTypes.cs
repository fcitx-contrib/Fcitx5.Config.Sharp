#nullable disable
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fcitx5.Config.Core.Transport.Dbus;

// Tmds.DBus models D-Bus STRUCT values as a [StructLayout(LayoutKind.Sequential)] class or struct
// with public instance fields, in declaration order. These types therefore use fields rather than
// properties, and nullable reference annotations are disabled because the marshaller assigns them.

/// <summary>Mirrors <c>(sssva{sv})</c>: one option of a configuration description.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusConfigOption
{
    public string Name;
    public string Type;
    public string Description;
    public object DefaultValue;
    public IDictionary<string, object> Properties;
}

/// <summary>Mirrors <c>(sa(sssva{sv}))</c>: one configuration group.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusConfigType
{
    public string Name;
    public DbusConfigOption[] Options;
}

/// <summary>Mirrors <c>(ssssssb)</c>: an entry of <c>AvailableInputMethods</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusInputMethodEntry
{
    public string UniqueName;
    public string Name;
    public string NativeName;
    public string Icon;
    public string Label;
    public string LanguageCode;
    public bool Configurable;
}

/// <summary>Mirrors <c>(sssibb)</c>: an entry of <c>GetAddons</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusAddonInfo
{
    public string UniqueName;
    public string Name;
    public string Comment;
    public int Category;
    public bool Configurable;
    public bool Enabled;
}

/// <summary>Mirrors <c>(sssibbbasas)</c>: an entry of <c>GetAddonsV2</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusAddonInfoV2
{
    public string UniqueName;
    public string Name;
    public string Comment;
    public int Category;
    public bool Configurable;
    public bool Enabled;
    public bool OnDemand;
    public string[] Dependencies;
    public string[] OptionalDependencies;
}

/// <summary>Mirrors <c>(sb)</c>: an entry of <c>SetAddonsState</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusAddonState
{
    public string UniqueName;
    public bool Enabled;
}

/// <summary>Mirrors <c>(ss)</c>: a name/layout pair.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusStringPair
{
    public string Key;
    public string Value;
}

/// <summary>Mirrors <c>(sssssssbsa{sv})</c>: an entry of <c>FullInputMethodGroupInfo</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusFullInputMethodEntry
{
    public string UniqueName;
    public string Name;
    public string NativeName;
    public string Icon;
    public string Label;
    public string LanguageCode;
    public string Addon;
    public bool Configurable;
    public string Layout;
    public IDictionary<string, object> Properties;
}

/// <summary>
/// The five output arguments of <c>FullInputMethodGroupInfo</c>:
/// <c>s s s a{sv} a(sssssssbsa{sv})</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusFullInputMethodGroupInfo
{
    public string GroupName;
    public string DefaultInputMethod;
    public string DefaultLayout;
    public IDictionary<string, object> Properties;
    public DbusFullInputMethodEntry[] Entries;
}

/// <summary>Mirrors <c>(ssas)</c>: a keyboard layout variant.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusLayoutVariant
{
    public string Variant;
    public string Description;
    public string[] Languages;
}

/// <summary>Mirrors <c>(ssasa(ssas))</c>: an entry of <c>AvailableKeyboardLayouts</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DbusLayoutInfo
{
    public string Layout;
    public string Description;
    public string[] Languages;
    public DbusLayoutVariant[] Variants;
}
