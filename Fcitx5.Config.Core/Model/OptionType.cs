using System;

namespace Fcitx5.Config.Core.Model;

/// <summary>
/// The base kind of a fcitx5 configuration option, derived from the type string reported by
/// <c>dumpDescription</c> (see <c>optiontypename.h</c> in fcitx5).
/// </summary>
public enum OptionKind
{
    /// <summary>Type string was empty or not recognised.</summary>
    Unknown = 0,

    /// <summary><c>Boolean</c></summary>
    Boolean,

    /// <summary><c>Integer</c></summary>
    Integer,

    /// <summary><c>String</c></summary>
    String,

    /// <summary><c>Key</c></summary>
    Key,

    /// <summary><c>Color</c></summary>
    Color,

    /// <summary><c>Version</c> (<c>SemanticVersion</c>)</summary>
    Version,

    /// <summary><c>I18NString</c></summary>
    I18NString,

    /// <summary><c>Enum</c></summary>
    Enum,

    /// <summary><c>External</c>: launches an external tool for configuration.</summary>
    External,

    /// <summary><c>SubConfig</c>: an <c>ExternalOption</c> that launches a sub configuration.</summary>
    SubConfig,

    /// <summary>A nested configuration group; the type string is the group name.</summary>
    Group,
}

/// <summary>
/// A parsed fcitx5 option type string, such as <c>Boolean</c>, <c>List|Key</c>,
/// <c>Optional|String</c> or a sub-configuration group name like <c>Hotkey$HotkeyConfig</c>.
/// </summary>
public sealed class OptionType
{
    private const string ListPrefix = "List|";
    private const string OptionalPrefix = "Optional|";

    private OptionType(string raw, OptionKind kind, bool isList, bool isOptional, OptionType? elementType, string? groupName)
    {
        Raw = raw;
        Kind = kind;
        IsList = isList;
        IsOptional = isOptional;
        ElementType = elementType;
        GroupName = groupName;
    }

    /// <summary>The original type string as reported by fcitx5.</summary>
    public string Raw { get; }

    /// <summary>The base kind, with <c>List|</c>/<c>Optional|</c> wrappers peeled off.</summary>
    public OptionKind Kind { get; }

    /// <summary>Whether this is a <c>List|...</c> option.</summary>
    public bool IsList { get; }

    /// <summary>Whether this is an <c>Optional|...</c> option.</summary>
    public bool IsOptional { get; }

    /// <summary>For <c>List|</c> and <c>Optional|</c>, the wrapped element type.</summary>
    public OptionType? ElementType { get; }

    /// <summary>For <see cref="OptionKind.Group"/>, the referenced group name.</summary>
    public string? GroupName { get; }

    /// <summary>A type that is not recognised.</summary>
    public static OptionType Unknown { get; } = new(string.Empty, OptionKind.Unknown, false, false, null, null);

    /// <summary>Parses a fcitx5 option type string.</summary>
    public static OptionType Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Unknown;
        }

        if (raw!.StartsWith(ListPrefix, StringComparison.Ordinal))
        {
            var element = Parse(raw.Substring(ListPrefix.Length));
            return new OptionType(raw, element.Kind, true, false, element, element.GroupName);
        }

        if (raw.StartsWith(OptionalPrefix, StringComparison.Ordinal))
        {
            var element = Parse(raw.Substring(OptionalPrefix.Length));
            return new OptionType(raw, element.Kind, false, true, element, element.GroupName);
        }

        switch (raw)
        {
            case "Boolean":
                return new OptionType(raw, OptionKind.Boolean, false, false, null, null);
            case "Integer":
                return new OptionType(raw, OptionKind.Integer, false, false, null, null);
            case "String":
                return new OptionType(raw, OptionKind.String, false, false, null, null);
            case "Key":
                return new OptionType(raw, OptionKind.Key, false, false, null, null);
            case "Color":
                return new OptionType(raw, OptionKind.Color, false, false, null, null);
            case "Version":
                return new OptionType(raw, OptionKind.Version, false, false, null, null);
            case "I18NString":
                return new OptionType(raw, OptionKind.I18NString, false, false, null, null);
            case "Enum":
                return new OptionType(raw, OptionKind.Enum, false, false, null, null);
            case "External":
                return new OptionType(raw, OptionKind.External, false, false, null, null);
            case "SubConfig":
                return new OptionType(raw, OptionKind.SubConfig, false, false, null, null);
            default:
                return new OptionType(raw, OptionKind.Group, false, false, null, raw);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Raw;
}
