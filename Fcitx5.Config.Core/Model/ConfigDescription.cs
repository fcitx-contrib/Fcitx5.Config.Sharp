using System;

namespace Fcitx5.Config.Core.Model;

/// <summary>
/// A single option inside a fcitx5 configuration description, as reported by
/// <c>dumpDBusConfigDescription</c>.
/// </summary>
public sealed class OptionDescription
{
    /// <summary>Creates an option description.</summary>
    public OptionDescription(
        string name,
        OptionType type,
        string description,
        RawConfigValue? defaultValue,
        OptionConstraints? constraints)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Description = description ?? string.Empty;
        DefaultValue = defaultValue;
        Constraints = constraints ?? OptionConstraints.Empty;
    }

    /// <summary>
    /// The option's path within its configuration. Single-segment in practice, but fcitx5
    /// technically allows a slash separated path.
    /// </summary>
    public string Name { get; }

    /// <summary>The parsed option type.</summary>
    public OptionType Type { get; }

    /// <summary>The localized human readable description.</summary>
    public string Description { get; }

    /// <summary>The default value, or <see langword="null"/> when the option has none.</summary>
    public RawConfigValue? DefaultValue { get; }

    /// <summary>Constraints and annotations for this option.</summary>
    public OptionConstraints Constraints { get; }

    /// <summary>The tooltip supplied by the server, when any.</summary>
    public string? Tooltip => Constraints.Tooltip;

    /// <summary>Whether the option holds multiple values.</summary>
    public bool IsMultiValue => Type.IsList || Type.IsOptional;

    /// <inheritdoc/>
    public override string ToString() => $"{Name}: {Type.Raw}";
}

/// <summary>
/// One configuration type (a "group") from the fcitx5 configuration description.
/// </summary>
/// <remarks>
/// Group names use <c>$</c> to separate nesting levels, for example
/// <c>Hotkey$HotkeyConfig</c>. A group reference option carries that name as its type string.
/// </remarks>
public sealed class ConfigTypeDescription
{
    /// <summary>Creates a group description.</summary>
    public ConfigTypeDescription(string name, OptionDescription[] options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Options = options ?? Array.Empty<OptionDescription>();
    }

    /// <summary>The group name, for example <c>GlobalConfig</c> or <c>Hotkey$HotkeyConfig</c>.</summary>
    public string Name { get; }

    /// <summary>The options of this group, in declaration order.</summary>
    public OptionDescription[] Options { get; }

    /// <summary>Returns the option with this name, or <see langword="null"/>.</summary>
    public OptionDescription? Find(string optionName)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.Name, optionName, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({Options.Length} options)";
}

/// <summary>
/// A parsed fcitx5 configuration description: the set of groups reported by the server.
/// </summary>
public sealed class ConfigDescription
{
    /// <summary>An empty description.</summary>
    public static ConfigDescription Empty { get; } = new(Array.Empty<ConfigTypeDescription>());

    /// <summary>Creates a description from its groups.</summary>
    public ConfigDescription(ConfigTypeDescription[] types)
    {
        Types = types ?? Array.Empty<ConfigTypeDescription>();
    }

    /// <summary>The groups, in the order reported by the server.</summary>
    public ConfigTypeDescription[] Types { get; }

    /// <summary>The root group (the first one), or <see langword="null"/> when empty.</summary>
    public ConfigTypeDescription? Root => Types.Length > 0 ? Types[0] : null;

    /// <summary>Returns the group with this exact name, or <see langword="null"/>.</summary>
    public ConfigTypeDescription? Find(string name)
    {
        foreach (var type in Types)
        {
            if (string.Equals(type.Name, name, StringComparison.Ordinal))
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the group referenced by a group-typed option, or <see langword="null"/> when the
    /// option does not reference a group that is present in this description.
    /// </summary>
    public ConfigTypeDescription? ResolveGroup(OptionDescription option)
    {
        if (option is null)
        {
            throw new ArgumentNullException(nameof(option));
        }

        var name = option.Type.GroupName;
        return name is null ? null : Find(name);
    }

    /// <summary>Looks up an option by name across every group.</summary>
    public OptionDescription? FindOption(string optionName)
    {
        foreach (var type in Types)
        {
            var option = type.Find(optionName);
            if (option is not null)
            {
                return option;
            }
        }

        return null;
    }
}
