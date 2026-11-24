using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fcitx5.Config.Core.Model;

/// <summary>A single value of an <c>Enum</c> option: the stored name plus its display label.</summary>
/// <remarks>
/// Equality compares <see cref="Name"/> only, because that is the value stored in the
/// configuration; the display label and position index are presentation details.
/// </remarks>
public readonly struct EnumItem : IEquatable<EnumItem>
{
    /// <summary>Creates an enum item.</summary>
    public EnumItem(string index, string name, string? displayName)
    {
        Index = index;
        Name = name;
        DisplayName = displayName ?? name;
    }

    /// <summary>The position key ("0", "1", ...) used by fcitx5.</summary>
    public string Index { get; }

    /// <summary>The value stored in the configuration.</summary>
    public string Name { get; }

    /// <summary>The localized label; falls back to <see cref="Name"/> when the server has none.</summary>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public bool Equals(EnumItem other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EnumItem other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        Name is null ? 0 : StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>Compares two enum items.</summary>
    public static bool operator ==(EnumItem left, EnumItem right) => left.Equals(right);

    /// <summary>Compares two enum items.</summary>
    public static bool operator !=(EnumItem left, EnumItem right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}

/// <summary>
/// The constraints and annotations attached to a fcitx5 option, decoded from the <c>a{sv}</c>
/// "properties" field of the configuration description.
/// </summary>
/// <remarks>
/// Constraint key names come from fcitx5's <c>option.h</c> and <c>enum.h</c>:
/// <c>IntMin</c>, <c>IntMax</c>, <c>Enum/&lt;n&gt;</c>, <c>EnumI18n/&lt;n&gt;</c>,
/// <c>AllowModifierLess</c>, <c>AllowModifierOnly</c>, <c>ListConstrain/*</c>,
/// <c>OptionalConstrain/*</c>, <c>Tooltip</c>, <c>ListDisplayOption</c>, <c>Font</c>,
/// <c>IsEnum</c>, <c>IsRegex</c>, <c>External</c> and <c>LaunchSubConfig</c>.
/// <para>
/// The public constructor takes a defensive copy, so a caller's tree can be reused. Decoders that
/// already own a freshly built tree use <see cref="FromOwned"/> to skip that copy.
/// </para>
/// </remarks>
public sealed class OptionConstraints
{
    private readonly RawConfigValue _raw;
    private IReadOnlyList<EnumItem>? _enumItems;

    /// <summary>Creates constraints from a decoded node; a null node means "no constraints".</summary>
    public OptionConstraints(RawConfigValue? raw)
        : this(raw ?? RawConfigValue.Empty, takeOwnership: false)
    {
    }

    private OptionConstraints(RawConfigValue raw, bool takeOwnership)
    {
        _raw = takeOwnership ? raw : raw.DeepClone();
    }

    /// <summary>Constraints with no entries.</summary>
    public static OptionConstraints Empty => new(RawConfigValue.Empty, takeOwnership: true);

    /// <summary>
    /// Creates constraints from a tree the caller owns and will not mutate afterwards. This avoids
    /// copying a tree that was just decoded; use the public constructor for caller owned trees.
    /// </summary>
    internal static OptionConstraints FromOwned(RawConfigValue raw)
    {
        if (raw is null)
        {
            throw new ArgumentNullException(nameof(raw));
        }

        return new OptionConstraints(raw, takeOwnership: true);
    }

    /// <summary>The raw constraint tree.</summary>
    public RawConfigValue Raw => _raw;

    /// <summary>The inclusive lower bound of an <see cref="OptionKind.Integer"/> option.</summary>
    public int? IntMin => ReadInt("IntMin");

    /// <summary>The inclusive upper bound of an <see cref="OptionKind.Integer"/> option.</summary>
    public int? IntMax => ReadInt("IntMax");

    /// <summary>The tooltip shown next to the option, when the server provided one.</summary>
    public string? Tooltip => ReadString("Tooltip");

    /// <summary>For list options, the name of the field used to display each item.</summary>
    public string? ListDisplayOption => ReadString("ListDisplayOption");

    /// <summary>Whether a string option should be rendered as a font selector.</summary>
    public bool IsFont => ReadBool("Font");

    /// <summary>Whether a string option should be rendered as a combo box of runtime values.</summary>
    public bool IsEnum => ReadBool("IsEnum");

    /// <summary>Whether a string option must hold a valid regular expression.</summary>
    public bool IsRegex => ReadBool("IsRegex");

    /// <summary>Whether a key option accepts modifier-only keys.</summary>
    public bool AllowModifierOnly =>
        ReadBool("AllowModifierOnly") || ListConstrain is { AllowModifierOnly: true };

    /// <summary>Whether a key option accepts keys without modifiers.</summary>
    public bool AllowModifierLess =>
        ReadBool("AllowModifierLess") || ListConstrain is { AllowModifierLess: true };

    /// <summary>The URI of the external tool for <see cref="OptionKind.External"/> options.</summary>
    public string? ExternalUri => ReadString("External");

    /// <summary>Whether an external option launches a sub configuration.</summary>
    public bool LaunchSubConfig => ReadBool("LaunchSubConfig");

    /// <summary>Constraints that apply to every element of a list option.</summary>
    public OptionConstraints? ListConstrain => Nested("ListConstrain");

    /// <summary>Constraints that apply to the value of an optional option.</summary>
    public OptionConstraints? OptionalConstrain => Nested("OptionalConstrain");

    /// <summary>
    /// The enum values in declaration order (from the <c>Enum</c> constraint), or an empty list.
    /// </summary>
    public IReadOnlyList<EnumItem> EnumValues => ReadEnum("Enum");

    /// <summary>
    /// The localized enum labels in declaration order (from the <c>EnumI18n</c> constraint).
    /// </summary>
    public IReadOnlyList<EnumItem> EnumI18nValues => ReadEnum("EnumI18n");

    /// <summary>
    /// The enum values paired with their localized labels, preferring the localized label.
    /// The result is computed once and reused.
    /// </summary>
    public IReadOnlyList<EnumItem> EnumItems() => _enumItems ??= BuildEnumItems();

    private IReadOnlyList<EnumItem> BuildEnumItems()
    {
        var values = EnumValues;
        var localized = EnumI18nValues;

        if (values.Count == 0)
        {
            return localized;
        }

        if (localized.Count == 0)
        {
            return values;
        }

        var result = new EnumItem[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var display = i < localized.Count ? localized[i].Name : null;
            result[i] = new EnumItem(values[i].Index, values[i].Name, display);
        }

        return result;
    }

    private OptionConstraints? Nested(string key)
    {
        // Children belong to _raw, which this instance already owns privately, so no copy is needed.
        var child = _raw.Find(key);
        return child is null ? null : FromOwned(child);
    }

    private string? ReadString(string key) => _raw.Find(key)?.Value;

    private bool ReadBool(string key) =>
        ReadString(key) is { } value &&
        (value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1");

    private int? ReadInt(string key)
    {
        var value = ReadString(key);
        return value is not null &&
               int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (int?)null;
    }

    private IReadOnlyList<EnumItem> ReadEnum(string key)
    {
        if (!_raw.TryFind(key, out var node) || !node.HasChildren)
        {
            return Array.Empty<EnumItem>();
        }

        var result = new EnumItem[node.ChildNames.Count];
        var index = 0;
        foreach (var pair in node.OrderedChildren)
        {
            result[index++] = new EnumItem(pair.Key, pair.Value.Value ?? string.Empty, null);
        }

        return result;
    }
}
