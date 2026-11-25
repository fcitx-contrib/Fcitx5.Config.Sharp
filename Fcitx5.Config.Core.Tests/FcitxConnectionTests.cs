using System;
using System.IO;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Transport.Dbus;

namespace Fcitx5.Config.Core.Tests;

public class FcitxConnectionTests
{
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var connection = new FcitxConnection();

        connection.Dispose();
        connection.Dispose();

        Assert.False(connection.IsAvailable);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var connection = new FcitxConnection();

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.False(connection.IsAvailable);
    }

    [Fact]
    public void ConnectAsync_AfterDispose_Throws()
    {
        var connection = new FcitxConnection();
        connection.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = connection.ConnectAsync();
        });
    }

    [Fact]
    public void Dispose_DoesNotRaiseAvailabilityChanged()
    {
        using var connection = new FcitxConnection();
        var raised = 0;
        connection.AvailabilityChanged += (_, _) => raised++;

        connection.Dispose();

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task ConnectAsync_ToABusThatIsNotThere_ReportsUnavailable()
    {
        var socket = Path.Combine(
            Path.GetTempPath(),
            "fcitx5-core-tests-missing-" + Guid.NewGuid().ToString("N"));
        using var connection = new FcitxConnection("unix:path=" + socket);

        var exception = await Assert.ThrowsAsync<FcitxUnavailableException>(() => connection.ConnectAsync());

        // The bus library's own exception is kept for diagnostics, but it is not the type a caller
        // has to handle.
        Assert.NotNull(exception.InnerException);
        Assert.False(connection.IsAvailable);

        // A failed connect is not cached, so the next attempt runs again instead of replaying it.
        await Assert.ThrowsAsync<FcitxUnavailableException>(() => connection.ConnectAsync());
    }
}
