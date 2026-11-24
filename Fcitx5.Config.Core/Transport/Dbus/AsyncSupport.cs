using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Cancellation helpers for D-Bus calls.
/// </summary>
/// <remarks>
/// <see cref="DBusConnection"/> has no cancellation token on its call methods, and a dispatched
/// call cannot be recalled anyway. These helpers stop waiting for the reply, which is what a caller
/// observes; the reply is still read and discarded when it arrives. Callers that must not reach the
/// daemon at all check the token before dispatching instead.
/// <c>Task.WaitAsync</c> would do the same but only exists from .NET 6, and this library also
/// targets netstandard2.1.
/// </remarks>
internal static class AsyncSupport
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        await WaitAsync(task, cancellationToken).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        return WaitAsync(task, cancellationToken);
    }

    private static async Task WaitAsync(Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        await task.ConfigureAwait(false);
    }
}
