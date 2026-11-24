using System;

namespace Fcitx5.Config.Core.Model;

/// <summary>
/// The category of a fcitx5 addon, matching <c>AddonCategory</c> in fcitx5's <c>addoninfo.h</c>.
/// </summary>
public enum AddonCategory
{
    /// <summary>Category was not reported.</summary>
    Unknown = -1,

    /// <summary>An input method engine.</summary>
    InputMethod = 0,

    /// <summary>A frontend.</summary>
    Frontend = 1,

    /// <summary>An addon loader.</summary>
    Loader = 2,

    /// <summary>A module.</summary>
    Module = 3,

    /// <summary>A user interface.</summary>
    Ui = 4,
}

/// <summary>
/// Metadata about a fcitx5 addon, from <c>GetAddonsV2</c>.
/// </summary>
public sealed class AddonInfo
{
    /// <summary>Creates addon metadata.</summary>
    public AddonInfo(
        string uniqueName,
        string name,
        string comment,
        int category,
        bool configurable,
        bool enabled,
        bool onDemand = false,
        string[]? dependencies = null,
        string[]? optionalDependencies = null)
    {
        UniqueName = uniqueName ?? throw new ArgumentNullException(nameof(uniqueName));
        Name = name ?? string.Empty;
        Comment = comment ?? string.Empty;
        Category = category;
        Configurable = configurable;
        Enabled = enabled;
        OnDemand = onDemand;
        Dependencies = dependencies ?? Array.Empty<string>();
        OptionalDependencies = optionalDependencies ?? Array.Empty<string>();
    }

    /// <summary>The addon's unique name, used in its configuration URI.</summary>
    public string UniqueName { get; }

    /// <summary>The localized display name.</summary>
    public string Name { get; }

    /// <summary>The localized one line description.</summary>
    public string Comment { get; }

    /// <summary>The numeric category; see <see cref="CategoryKind"/>.</summary>
    public int Category { get; }

    /// <summary>The category as an enum.</summary>
    public AddonCategory CategoryKind =>
        Category >= 0 && Category <= 4 ? (AddonCategory)Category : AddonCategory.Unknown;

    /// <summary>Whether the addon exposes a configuration UI.</summary>
    public bool Configurable { get; }

    /// <summary>Whether the addon is currently enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Whether the addon is loaded on demand rather than at startup.</summary>
    public bool OnDemand { get; }

    /// <summary>Required addons.</summary>
    public string[] Dependencies { get; }

    /// <summary>The optional addons.</summary>
    public string[] OptionalDependencies { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{UniqueName} ({Name})";
}
