using System;

namespace Fcitx5.Config.Core.Model;

/// <summary>The runtime state of the fcitx5 instance, from <c>Controller1.State</c>.</summary>
public enum FcitxState
{
    /// <summary>No input context is using fcitx5.</summary>
    Closed = 0,

    /// <summary>Fcitx5 is running but the current input method is not active.</summary>
    Inactive = 1,

    /// <summary>The current input method is active.</summary>
    Active = 2,
}

/// <summary>
/// An input method known to fcitx5, from <c>AvailableInputMethods</c>.
/// </summary>
public sealed class InputMethodEntry
{
    /// <summary>Creates an input method entry.</summary>
    public InputMethodEntry(
        string uniqueName,
        string name,
        string nativeName,
        string icon,
        string label,
        string languageCode,
        bool configurable)
    {
        UniqueName = uniqueName ?? throw new ArgumentNullException(nameof(uniqueName));
        Name = name ?? string.Empty;
        NativeName = nativeName ?? string.Empty;
        Icon = icon ?? string.Empty;
        Label = label ?? string.Empty;
        LanguageCode = languageCode ?? string.Empty;
        Configurable = configurable;
    }

    /// <summary>The unique name, used to select the input method and in its configuration URI.</summary>
    public string UniqueName { get; }

    /// <summary>The localized display name.</summary>
    public string Name { get; }

    /// <summary>The name in the input method's own language.</summary>
    public string NativeName { get; }

    /// <summary>The icon name.</summary>
    public string Icon { get; }

    /// <summary>An additional label, often empty.</summary>
    public string Label { get; }

    /// <summary>The ISO language code, for example <c>zh_CN</c>.</summary>
    public string LanguageCode { get; }

    /// <summary>Whether the input method has a configuration UI.</summary>
    public bool Configurable { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{UniqueName} ({Name})";
}

/// <summary>
/// An input method entry as returned inside a group, from <c>FullInputMethodGroupInfo</c>.
/// </summary>
public sealed class FullInputMethodEntry
{
    /// <summary>Creates a full input method entry.</summary>
    public FullInputMethodEntry(
        string uniqueName,
        string name,
        string nativeName,
        string icon,
        string label,
        string languageCode,
        string addon,
        bool configurable,
        string layout,
        RawConfigValue? properties)
    {
        UniqueName = uniqueName ?? throw new ArgumentNullException(nameof(uniqueName));
        Name = name ?? string.Empty;
        NativeName = nativeName ?? string.Empty;
        Icon = icon ?? string.Empty;
        Label = label ?? string.Empty;
        LanguageCode = languageCode ?? string.Empty;
        Addon = addon ?? string.Empty;
        Configurable = configurable;
        Layout = layout ?? string.Empty;
        Properties = properties ?? RawConfigValue.Empty;
    }

    /// <summary>The unique name of the input method.</summary>
    public string UniqueName { get; }

    /// <summary>The localized display name.</summary>
    public string Name { get; }

    /// <summary>The name in the input method's own language.</summary>
    public string NativeName { get; }

    /// <summary>The icon name.</summary>
    public string Icon { get; }

    /// <summary>An additional label.</summary>
    public string Label { get; }

    /// <summary>The ISO language code.</summary>
    public string LanguageCode { get; }

    /// <summary>The addon that provides this input method.</summary>
    public string Addon { get; }

    /// <summary>Whether the input method has a configuration UI.</summary>
    public bool Configurable { get; }

    /// <summary>The keyboard layout bound to this entry inside the group.</summary>
    public string Layout { get; }

    /// <summary>Extra properties reported for this entry.</summary>
    public RawConfigValue Properties { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{UniqueName} ({Name})";
}

/// <summary>A keyboard layout variant, from <c>AvailableKeyboardLayouts</c>.</summary>
public readonly struct KeyboardLayoutVariant : IEquatable<KeyboardLayoutVariant>
{
    /// <summary>Creates a layout variant.</summary>
    public KeyboardLayoutVariant(string variant, string description, string[]? languages)
    {
        Variant = variant ?? string.Empty;
        Description = description ?? string.Empty;
        Languages = languages ?? Array.Empty<string>();
    }

    /// <summary>The variant identifier, for example <c>intl</c>.</summary>
    public string Variant { get; }

    /// <summary>The localized description.</summary>
    public string Description { get; }

    /// <summary>The languages the variant applies to.</summary>
    public string[] Languages { get; }

    /// <inheritdoc/>
    public bool Equals(KeyboardLayoutVariant other) =>
        string.Equals(Variant, other.Variant, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeyboardLayoutVariant other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        Variant is null ? 0 : StringComparer.Ordinal.GetHashCode(Variant);

    /// <summary>Compares two layout variants by their variant identifier.</summary>
    public static bool operator ==(KeyboardLayoutVariant left, KeyboardLayoutVariant right) =>
        left.Equals(right);

    /// <summary>Compares two layout variants by their variant identifier.</summary>
    public static bool operator !=(KeyboardLayoutVariant left, KeyboardLayoutVariant right) =>
        !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() =>
        string.IsNullOrEmpty(Description) ? Variant : $"{Variant} ({Description})";
}

/// <summary>A keyboard layout, from <c>AvailableKeyboardLayouts</c>.</summary>
public sealed class KeyboardLayoutInfo
{
    /// <summary>Creates a keyboard layout.</summary>
    public KeyboardLayoutInfo(
        string layout,
        string description,
        string[]? languages,
        KeyboardLayoutVariant[]? variants)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Description = description ?? string.Empty;
        Languages = languages ?? Array.Empty<string>();
        Variants = variants ?? Array.Empty<KeyboardLayoutVariant>();
    }

    /// <summary>The layout identifier, for example <c>us</c>.</summary>
    public string Layout { get; }

    /// <summary>The localized description.</summary>
    public string Description { get; }

    /// <summary>The languages the layout applies to.</summary>
    public string[] Languages { get; }

    /// <summary>The variants of this layout.</summary>
    public KeyboardLayoutVariant[] Variants { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Layout} ({Description})";
}

/// <summary>
/// An input method group: an ordered list of input methods plus a default layout.
/// </summary>
public sealed class InputMethodGroup
{
    /// <summary>Creates a group.</summary>
    public InputMethodGroup(
        string name,
        string defaultInputMethod,
        string defaultLayout,
        FullInputMethodEntry[] items,
        RawConfigValue? properties = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DefaultInputMethod = defaultInputMethod ?? string.Empty;
        DefaultLayout = defaultLayout ?? string.Empty;
        Items = items ?? Array.Empty<FullInputMethodEntry>();
        Properties = properties ?? RawConfigValue.Empty;
    }

    /// <summary>The group name.</summary>
    public string Name { get; }

    /// <summary>The input method selected by default when the group is activated.</summary>
    public string DefaultInputMethod { get; }

    /// <summary>The keyboard layout used by default for this group.</summary>
    public string DefaultLayout { get; }

    /// <summary>The input methods of this group, in order.</summary>
    public FullInputMethodEntry[] Items { get; }

    /// <summary>Extra properties reported for this group.</summary>
    public RawConfigValue Properties { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({Items.Length} items)";
}
