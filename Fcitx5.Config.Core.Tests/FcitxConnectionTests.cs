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
}
