using System;
using System.Linq;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Fcitx;
using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// Tests that talk to a real fcitx5 instance over the session bus. They are skipped when the
/// platform is not Linux or when fcitx5 is not running, so the suite stays green on other setups.
/// </summary>
public class DbusIntegrationTests
{
    private const string WriteOptInVariable = "FCITX5_INTEGRATION_WRITE";

    private static async Task<FcitxController> ConnectAsync()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "The D-Bus transport only works on Linux.");

        var controller = await FcitxController.ConnectDbusAsync();
        var available = await WaitForAvailabilityAsync(controller);
        Skip.IfNot(available, "fcitx5 is not running on the session bus.");

        return controller;
    }

    private static async Task<bool> WaitForAvailabilityAsync(FcitxController controller)
    {
        for (var i = 0; i < 40; i++)
        {
            if (controller.IsAvailable)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return controller.IsAvailable;
    }

    [SkippableFact]
    public async Task Connects_And_ReportsFullCapabilities()
    {
        using var controller = await ConnectAsync();

        Assert.Equal(FcitxCapabilities.Full, controller.Capabilities);
        Assert.True(controller.IsAvailable);
    }

    [SkippableFact]
    public async Task GetState_ReturnsKnownValue()
    {
        using var controller = await ConnectAsync();

        var state = await controller.GetStateAsync();

        Assert.True(Enum.IsDefined(typeof(FcitxState), state));
    }

    [SkippableFact]
    public async Task GetAddons_ReturnsConsistentEntries()
    {
        using var controller = await ConnectAsync();

        var addons = await controller.GetAddonsAsync();

        Assert.NotEmpty(addons);
        Assert.Contains(addons, addon => addon.UniqueName == "keyboard");
        Assert.All(addons, addon => Assert.False(string.IsNullOrEmpty(addon.UniqueName)));
        Assert.Equal(
            addons.Count,
            addons.Select(addon => addon.UniqueName).Distinct().Count());
    }

    [SkippableFact]
    public async Task GetInputMethods_ReturnsConsistentEntries()
    {
        using var controller = await ConnectAsync();

        var inputMethods = await controller.GetInputMethodsAsync();

        Assert.NotEmpty(inputMethods);
        Assert.All(inputMethods, entry => Assert.False(string.IsNullOrEmpty(entry.UniqueName)));
        Assert.Equal(
            inputMethods.Count,
            inputMethods.Select(entry => entry.UniqueName).Distinct().Count());
    }

    [SkippableFact]
    public async Task GetInputMethodGroup_ResolvesCurrentGroup()
    {
        using var controller = await ConnectAsync();

        var groups = await controller.GetInputMethodGroupsAsync();
        var current = await controller.GetCurrentInputMethodGroupAsync();
        var group = await controller.GetInputMethodGroupAsync();

        Assert.NotEmpty(groups);
        Assert.Contains(current, groups);
        Assert.Equal(current, group.Name);
        Assert.NotEmpty(group.Items);
        Assert.All(group.Items, item => Assert.False(string.IsNullOrEmpty(item.UniqueName)));
    }

    [SkippableFact]
    public async Task GetGlobalConfig_DescribesGroupsAndOptions()
    {
        using var controller = await ConnectAsync();

        var snapshot = await controller.GetGlobalConfigAsync();

        Assert.NotEmpty(snapshot.Description.Types);
        Assert.Equal("GlobalConfig", snapshot.Description.Root!.Name);
        Assert.All(snapshot.Description.Types, type => Assert.False(string.IsNullOrEmpty(type.Name)));

        var activeByDefault = snapshot.FindOption("ActiveByDefault");
        Assert.NotNull(activeByDefault);
        Assert.Equal(OptionKind.Boolean, activeByDefault!.Type.Kind);
        Assert.Equal("False", activeByDefault.DefaultValue!.Value);

        var triggerKeys = snapshot.FindOption("TriggerKeys");
        Assert.NotNull(triggerKeys);
        Assert.True(triggerKeys!.Type.IsList);
        Assert.Equal(OptionKind.Key, triggerKeys.Type.Kind);
    }

    [SkippableFact]
    public async Task GetGlobalConfig_EnumOptionsExposeValues()
    {
        using var controller = await ConnectAsync();

        var snapshot = await controller.GetGlobalConfigAsync();
        var enumOption = snapshot.Description.Types
            .SelectMany(type => type.Options)
            .First(option => option.Type.Kind == OptionKind.Enum);

        var items = enumOption.Constraints.EnumItems();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.False(string.IsNullOrEmpty(item.Name)));
    }

    [SkippableFact]
    public async Task GetConfig_UnknownAddon_ReportsInvalidArgument()
    {
        using var controller = await ConnectAsync();

        var exception = await Assert.ThrowsAsync<FcitxErrorException>(
            () => controller.GetAddonConfigAsync("definitely-not-an-addon"));

        // fcitx5 answers a requested addon it does not know with InvalidArgs, not with a
        // "not found" of its own, so the kind follows the daemon rather than being reinterpreted.
        Assert.Equal(FcitxErrorKind.InvalidArgument, exception.Kind);
        Assert.Equal("org.freedesktop.DBus.Error.InvalidArgs", exception.TransportErrorName);
        Assert.Equal("Addon does not exist.", exception.Message);
    }

    [SkippableFact]
    public async Task GetConfig_ConfigurableAddon_Resolves()
    {
        using var controller = await ConnectAsync();

        var addons = await controller.GetAddonsAsync();
        var addon = addons.FirstOrDefault(a => a.Configurable && a.CategoryKind == AddonCategory.Module);
        Skip.If(addon is null, "No configurable module addon is installed.");

        var snapshot = await controller.GetAddonConfigAsync(addon!.UniqueName);

        Assert.Equal(FcitxConfigUri.Addon(addon!.UniqueName), snapshot.Uri);
        Assert.NotEmpty(snapshot.Description.Types);
    }

    /// <summary>
    /// Writes configuration back unchanged. Disabled by default because it mutates the user's
    /// fcitx5 configuration files (values stay identical, but fcitx5 rewrites the files).
    /// Set <c>FCITX5_INTEGRATION_WRITE=1</c> to enable.
    /// </summary>
    [SkippableFact]
    public async Task SetConfig_RoundTripsUnchanged()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable(WriteOptInVariable) == "1",
            $"Set {WriteOptInVariable}=1 to run configuration write tests.");

        using var controller = await ConnectAsync();

        var addons = await controller.GetAddonsAsync();
        var addon = addons.First(a => a.Configurable && a.CategoryKind == AddonCategory.Module);

        var before = await controller.GetAddonConfigAsync(addon.UniqueName);
        await controller.SetAddonConfigAsync(addon.UniqueName, before.Value.DeepClone());
        var after = await controller.GetAddonConfigAsync(addon.UniqueName);

        Assert.True(
            before.Value.DeepEquals(after.Value),
            $"Configuration changed after a no-op write.{Environment.NewLine}" +
            $"before: {before.Value}{Environment.NewLine}after: {after.Value}");
    }
}
