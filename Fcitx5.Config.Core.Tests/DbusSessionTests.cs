using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// Tests the D-Bus transport against a fake <c>org.fcitx.Fcitx.Controller1</c> on a private bus.
/// </summary>
/// <remarks>
/// Running against a real bus (rather than a mocked proxy) means the payloads go through the real
/// marshaller in both directions, so an encoding mistake fails here instead of only on a machine
/// that happens to run fcitx5.
/// </remarks>
[Collection(PrivateBusCollection.Name)]
public class DbusSessionTests
{
    private readonly PrivateBusFixture _bus;

    public DbusSessionTests(PrivateBusFixture bus)
    {
        _bus = bus;
    }

    [SkippableTheory]
    [InlineData(0, FcitxState.Closed)]
    [InlineData(1, FcitxState.Inactive)]
    [InlineData(2, FcitxState.Active)]
    public async Task GetStateAsync_MapsDaemonState(int raw, FcitxState expected)
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        fake.State = raw;

        Assert.Equal(expected, await session.GetStateAsync());
    }

    [SkippableFact]
    public async Task GetAddonsAsync_ReadsModernFields()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        var addons = await session.GetAddonsAsync();

        Assert.Equal(2, addons.Count);
        var pinyin = addons[0];
        Assert.Equal("pinyin", pinyin.UniqueName);
        Assert.Equal(AddonCategory.InputMethod, pinyin.CategoryKind);
        Assert.True(pinyin.Configurable);
        Assert.False(pinyin.OnDemand);
        Assert.Equal(new[] { "core", "dbus" }, pinyin.Dependencies);
        Assert.Equal(new[] { "quickphrase" }, pinyin.OptionalDependencies);
        Assert.Equal(AddonCategory.Frontend, addons[1].CategoryKind);
    }

    [SkippableFact]
    public async Task GetAddonsAsync_FallsBackWhenTheDaemonLacksGetAddonsV2()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        fake.AddonsMode = AddonReplyMode.UnknownMethod;

        var addons = await session.GetAddonsAsync();

        var addon = Assert.Single(addons);
        Assert.Equal("pinyin", addon.UniqueName);
        Assert.Empty(addon.Dependencies);
    }

    [SkippableFact]
    public async Task GetAddonsAsync_PropagatesOtherErrors()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        fake.AddonsMode = AddonReplyMode.Error;

        var exception = await Assert.ThrowsAsync<FcitxErrorException>(
            () => session.GetAddonsAsync());

        // The fake reports an error name outside the standard set, so the kind falls back to
        // Unknown rather than guessing, and the name stays available for the caller to inspect.
        Assert.Equal(FcitxErrorKind.Unknown, exception.Kind);
        Assert.Equal("org.fcitx.Test.Failure", exception.TransportErrorName);
        Assert.Equal("GetAddonsV2 failed on purpose.", exception.Message);
    }

    [SkippableFact]
    public async Task Calls_WithNoDaemonOnTheBus_ReportUnavailable()
    {
        await using var session = await ConnectAsync();

        // The private bus is running but nothing owns org.fcitx.Fcitx5, which is exactly what a
        // configuration manager sees before the daemon starts.
        Assert.False(session.IsAvailable);

        var exception = await Assert.ThrowsAsync<FcitxUnavailableException>(
            () => session.GetStateAsync());

        Assert.Equal("org.freedesktop.DBus.Error.ServiceUnknown", exception.TransportErrorName);
    }

    [SkippableFact]
    public async Task GetConfigAsync_DecodesValueAndDescription()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        ConfigureFakeConfig(fake);

        var snapshot = await session.GetConfigAsync("fcitx://config/global");

        Assert.Equal("fcitx://config/global", snapshot.Uri);
        Assert.Equal(
            new[] { "Control+space", "Zenkaku_Hankaku" },
            snapshot.Value.FindPath("Hotkey/TriggerKeys")!.ToStringList());
        Assert.Equal("False", snapshot.Value.FindPath("Behavior/ActiveByDefault")!.Value);

        Assert.Equal(2, snapshot.Description.Types.Length);
        Assert.Equal("GlobalConfig", snapshot.Description.Root!.Name);

        var groupReference = snapshot.Description.FindOption("Hotkey")!;
        Assert.Equal(OptionKind.Group, groupReference.Type.Kind);
        Assert.Equal("Hotkey$HotkeyConfig", snapshot.Description.ResolveGroup(groupReference)!.Name);

        var triggerKeys = snapshot.Description.FindOption("TriggerKeys")!;
        Assert.True(triggerKeys.Type.IsList);
        Assert.Equal(OptionKind.Key, triggerKeys.Type.Kind);
        Assert.Equal("切换输入法", triggerKeys.Description);
        Assert.Equal(
            new[] { "Control+space", "Zenkaku_Hankaku" },
            triggerKeys.DefaultValue!.ToStringList());
        Assert.True(triggerKeys.Constraints.ListConstrain!.AllowModifierLess);
    }

    [SkippableFact]
    public async Task SetConfigAsync_PreservesEntryOrderOnTheWire()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        var value = new RawConfigValue();
        value.SetPath("Hotkey/TriggerKeys/1", "Zenkaku_Hankaku");
        value.SetPath("Hotkey/TriggerKeys/0", "Control+space");

        await session.SetConfigAsync("fcitx://config/global", value);

        Assert.Equal("fcitx://config/global", fake.LastSetConfigUri);
        Assert.True(value.DeepEquals(fake.LastSetConfigValue));
        Assert.Equal(
            new[] { "1", "0" },
            fake.LastSetConfigValue!.FindPath("Hotkey/TriggerKeys")!.ChildNames);
    }

    [SkippableFact]
    public async Task SetAddonsEnabledAsync_SendsEveryState()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        await session.SetAddonsEnabledAsync(new Dictionary<string, bool>
        {
            ["pinyin"] = true,
            ["xim"] = false,
        });

        Assert.NotNull(fake.LastAddonStates);
        Assert.True(fake.LastAddonStates!["pinyin"]);
        Assert.False(fake.LastAddonStates!["xim"]);
    }

    [SkippableFact]
    public async Task GetInputMethodGroupAsync_MapsEntries()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        fake.Group = new InputMethodGroup(
            "Default",
            "pinyin",
            "us",
            new[]
            {
                new FullInputMethodEntry("pinyin", "Pinyin", "拼音", "pinyin", "", "zh_CN", "pinyin", true, "us", null),
            });

        var group = await session.GetInputMethodGroupAsync("Default");

        Assert.Equal("Default", group.Name);
        Assert.Equal("pinyin", group.DefaultInputMethod);
        Assert.Equal("us", group.DefaultLayout);
        var item = Assert.Single(group.Items);
        Assert.Equal("pinyin", item.UniqueName);
        Assert.True(item.Configurable);
        Assert.Equal("us", item.Layout);
    }

    [SkippableFact]
    public async Task GetInputMethodGroupAsync_ReportsMissingGroups()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        fake.Group = null;

        var exception = await Assert.ThrowsAsync<FcitxErrorException>(
            () => session.GetInputMethodGroupAsync("Missing"));

        Assert.Equal(FcitxErrorKind.NotFound, exception.Kind);
        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task SetInputMethodGroupAsync_SendsItems()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();
        var group = new InputMethodGroup(
            "Work",
            "pinyin",
            "us",
            new[]
            {
                new FullInputMethodEntry("pinyin", "", "", "", "", "", "", false, "us", null),
                new FullInputMethodEntry("keyboard-us", "", "", "", "", "", "", false, "", null),
            });

        await session.SetInputMethodGroupAsync(group);

        Assert.NotNull(fake.LastSavedGroup);
        Assert.Equal("Work", fake.LastSavedGroup!.Name);
        Assert.Equal("us", fake.LastSavedGroup.DefaultLayout);
        Assert.Equal(new[] { "pinyin", "keyboard-us" }, fake.LastSavedGroup.Items.Select(i => i.UniqueName));
        Assert.Equal("us", fake.LastSavedGroup.Items[0].Layout);
    }

    [SkippableFact]
    public async Task GetInputMethodsAsync_MapsEntries()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        var entries = await session.GetInputMethodsAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal("pinyin", entries[0].UniqueName);
        Assert.Equal("zh_CN", entries[0].LanguageCode);
        Assert.True(entries[0].Configurable);
        Assert.False(entries[1].Configurable);
    }

    [SkippableFact]
    public async Task GetKeyboardLayoutsAsync_MapsVariants()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        var layouts = await session.GetKeyboardLayoutsAsync();

        var layout = Assert.Single(layouts);
        Assert.Equal("us", layout.Layout);
        Assert.Equal(new[] { "en" }, layout.Languages);
        Assert.Equal(new[] { "intl", "dvorak" }, layout.Variants.Select(v => v.Variant));
        Assert.Equal("Dvorak", layout.Variants[1].Description);
    }

    [SkippableFact]
    public async Task ConnectAsync_KnowsAvailabilityImmediately()
    {
        await using var fake = await StartFakeAsync();

        await using var session = new DbusSession(new FcitxConnection(_bus.Address!));
        await session.ConnectAsync();

        Assert.True(session.IsAvailable);
    }

    [SkippableFact]
    public async Task Availability_FollowsTheWellKnownName()
    {
        await using var session = await ConnectAsync();
        Assert.False(session.IsAvailable);

        await using (var fake = await StartFakeAsync())
        {
            Assert.True(await WaitForAvailabilityAsync(session, expected: true));
        }

        Assert.True(await WaitForAvailabilityAsync(session, expected: false));
    }

    [SkippableFact]
    public async Task CancelledWrite_NeverReachesTheDaemon()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.SetConfigAsync("fcitx://config/global", new RawConfigValue("True"), cancellation.Token));

        // A cancelled save must not overwrite the user's configuration.
        Assert.Null(fake.LastSetConfigUri);
    }

    [SkippableFact]
    public async Task ConfigCalls_RejectAnEmptyUriBeforeTouchingTheDaemon()
    {
        await using var session = await ConnectAsync();
        await using var fake = await StartFakeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => session.GetConfigAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(
            () => session.SetConfigAsync(string.Empty, new RawConfigValue("True")));

        Assert.Null(fake.LastSetConfigUri);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<DbusSession> ConnectAsync()
    {
        Skip.If(_bus.Address is null, "dbus-daemon is not available, so the private bus cannot be started.");

        var session = new DbusSession(new FcitxConnection(_bus.Address));
        await session.ConnectAsync();
        return session;
    }

    private async Task<FakeFcitx> StartFakeAsync()
    {
        Skip.If(_bus.Address is null, "dbus-daemon is not available, so the private bus cannot be started.");
        return await FakeFcitx.StartAsync(_bus.Address!);
    }

    private static async Task<bool> WaitForAvailabilityAsync(DbusSession session, bool expected)
    {
        var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnAvailabilityChanged(object? sender, bool available)
        {
            if (available == expected)
            {
                reached.TrySetResult(true);
            }
        }

        // Subscribe before reading the current value: the change may happen in between, and the
        // check alone would then wait for an event that already fired.
        session.AvailabilityChanged += OnAvailabilityChanged;
        try
        {
            if (session.IsAvailable == expected)
            {
                return true;
            }

            var completed = await Task.WhenAny(reached.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            return completed == reached.Task;
        }
        finally
        {
            session.AvailabilityChanged -= OnAvailabilityChanged;
        }
    }

    private static void ConfigureFakeConfig(FakeFcitx fake)
    {
        var value = new RawConfigValue();
        value.SetPath("Behavior/ActiveByDefault", "False");
        value.SetPath("Hotkey/TriggerKeys/0", "Control+space");
        value.SetPath("Hotkey/TriggerKeys/1", "Zenkaku_Hankaku");
        fake.ConfigValue = value;

        var triggerKeysDefault = RawConfigValue.FromList(new[] { "Control+space", "Zenkaku_Hankaku" });
        var triggerKeysProperties = new RawConfigValue();
        var listConstraints = triggerKeysProperties.GetOrAddChild("ListConstrain");
        listConstraints.SetChild("AllowModifierLess", "True");
        listConstraints.SetChild("AllowModifierOnly", "True");

        fake.ConfigGroups.Add(new FakeGroup(
            "GlobalConfig",
            new FakeOption("Hotkey", "Hotkey$HotkeyConfig", "快捷键", new RawConfigValue("")),
            new FakeOption("ActiveByDefault", "Boolean", "默认激活输入法", new RawConfigValue("False"))));
        fake.ConfigGroups.Add(new FakeGroup(
            "Hotkey$HotkeyConfig",
            new FakeOption(
                "TriggerKeys",
                "List|Key",
                "切换输入法",
                triggerKeysDefault,
                triggerKeysProperties)));
    }
}
