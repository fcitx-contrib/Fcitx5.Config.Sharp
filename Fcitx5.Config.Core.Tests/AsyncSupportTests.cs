using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Transport.Dbus;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

public class AsyncSupportTests
{
    [Fact]
    public async Task WithCancellation_ReturnsTheResultWhenTheTaskWins()
    {
        Assert.Equal(42, await Task.FromResult(42).WithCancellation(default));
    }

    [Fact]
    public async Task WithCancellation_ThrowsWhenTheTokenFiresFirst()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();

        var wait = pending.Task.WithCancellation(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public async Task WithCancellation_ThrowsForAnAlreadyCancelledToken()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.Task.WithCancellation(cancellation.Token));
    }

    [Fact]
    public async Task WithCancellation_PropagatesFaults()
    {
        var failed = Task.FromException(new InvalidOperationException("boom"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => failed.WithCancellation(CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task WithCancellation_AppliesToTheNonGenericOverload()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();

        var wait = ((Task)pending.Task).WithCancellation(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
