using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Fcitx;
using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// A session that only implements what the tests need, used to verify the facade's capability
/// gating without a live daemon.
/// </summary>
internal sealed class FakeSession : IFcitxSession
{
    public FakeSession(FcitxCapabilities capabilities)
    {
        Capabilities = capabilities;
    }

    public FcitxCapabilities Capabilities { get; }

    public bool IsAvailable { get; set; } = true;

    public event EventHandler<bool>? AvailabilityChanged;

    public List<string> RequestedUris { get; } = new();

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ConfigSnapshot> GetConfigAsync(string uri, CancellationToken cancellationToken = default)
    {
        RequestedUris.Add(uri);
        return Task.FromResult(new ConfigSnapshot(uri, new RawConfigValue("x"), ConfigDescription.Empty));
    }

    public Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default)
    {
        RequestedUris.Add(uri);
        return Task.CompletedTask;
    }

    public void SetAvailability(bool available)
    {
        IsAvailable = available;
        AvailabilityChanged?.Invoke(this, available);
    }

    public Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AddonInfo>>(new[]
        {
            new AddonInfo("pinyin", "Pinyin", "", 0, true, true),
            new AddonInfo("xim", "XIM", "", 1, false, true),
        });

    public Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<InputMethodEntry>> GetInputMethodsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InputMethodEntry>>(Array.Empty<InputMethodEntry>());

    public Task<IReadOnlyList<string>> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("Default");

    public Task<InputMethodGroup> GetInputMethodGroupAsync(
        string? groupName = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new InputMethodGroup("Default", "pinyin", "us", Array.Empty<FullInputMethodEntry>()));

    public Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<KeyboardLayoutInfo>> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KeyboardLayoutInfo>>(Array.Empty<KeyboardLayoutInfo>());

    public Task<FcitxState> GetStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(FcitxState.Active);

    public Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("pinyin");

    public Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ToggleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReloadConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ExitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}

public class FcitxControllerTests
{
    [Fact]
    public void Supports_ReflectsSessionCapabilities()
    {
        var controller = new FcitxController(new FakeSession(FcitxCapabilities.ConfigRead));

        Assert.True(controller.Supports(FcitxCapabilities.ConfigRead));
        Assert.False(controller.Supports(FcitxCapabilities.ConfigRead | FcitxCapabilities.ConfigWrite));
    }

    [Fact]
    public async Task UnsupportedFeature_ThrowsNotSupportedException()
    {
        var controller = new FcitxController(new FakeSession(FcitxCapabilities.ConfigRead));

        await Assert.ThrowsAsync<NotSupportedException>(() => controller.GetAddonsAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => controller.GetInputMethodsAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => controller.RestartAsync());
    }

    [Fact]
    public async Task SupportedFeature_IsForwarded()
    {
        var session = new FakeSession(FcitxCapabilities.ConfigRead | FcitxCapabilities.ConfigWrite);
        var controller = new FcitxController(session);

        var snapshot = await controller.GetGlobalConfigAsync();
        await controller.SetGlobalConfigAsync(new RawConfigValue("y"));

        Assert.Equal("fcitx://config/global", snapshot.Uri);
        Assert.Equal(
            new[] { "fcitx://config/global", "fcitx://config/global" },
            session.RequestedUris);
    }

    [Fact]
    public async Task ConvenienceUriHelpers_BuildExpectedUris()
    {
        var session = new FakeSession(FcitxCapabilities.ConfigRead | FcitxCapabilities.ConfigWrite);
        var controller = new FcitxController(session);

        await controller.GetAddonConfigAsync("pinyin");
        await controller.GetInputMethodConfigAsync("rime");

        Assert.Equal(
            new[] { "fcitx://config/addon/pinyin", "fcitx://config/inputmethod/rime" },
            session.RequestedUris);
    }

    [Fact]
    public async Task GetAddonsAsync_ByCategory_FiltersResults()
    {
        var controller = new FcitxController(new FakeSession(FcitxCapabilities.AddonEnumeration));

        var inputMethods = await controller.GetAddonsAsync(AddonCategory.InputMethod);
        var frontends = await controller.GetAddonsAsync(AddonCategory.Frontend);

        Assert.Single(inputMethods);
        Assert.Equal("pinyin", inputMethods[0].UniqueName);
        Assert.Single(frontends);
        Assert.Equal("xim", frontends[0].UniqueName);
    }

    [Fact]
    public void AvailabilityChanged_IsForwarded()
    {
        var session = new FakeSession(FcitxCapabilities.Full);
        var controller = new FcitxController(session);
        bool? observed = null;
        controller.AvailabilityChanged += (_, available) => observed = available;

        session.SetAvailability(false);

        Assert.False(observed);
        Assert.False(controller.IsAvailable);
    }

    [Fact]
    public async Task ConnectAsync_AfterSyncDispose_Throws()
    {
        var controller = new FcitxController(new FakeSession(FcitxCapabilities.Full));

        controller.Dispose();
        controller.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.ConnectAsync());
    }

    [Fact]
    public async Task ConnectAsync_AfterAsyncDispose_Throws()
    {
        var controller = new FcitxController(new FakeSession(FcitxCapabilities.Full));

        await controller.DisposeAsync();
        await controller.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.ConnectAsync());
    }

    [Fact]
    public async Task AwaitUsing_WorksWithTheFacade()
    {
        await using var controller = new FcitxController(new FakeSession(FcitxCapabilities.Full));

        Assert.True(controller.IsAvailable);
    }
}
