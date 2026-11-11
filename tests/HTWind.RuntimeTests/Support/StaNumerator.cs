using System.Windows;
using System.Windows.Threading;

namespace HTWind.RuntimeTests.Support;

/// <summary>
///     Runs async work on a single dedicated STA thread with a WPF dispatcher, so WebView2 and
///     WPF controls can be created and driven without polluting the test runner thread.
/// </summary>
public sealed class StaRunner : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource<Dispatcher> _dispatcherReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public StaRunner()
    {
        _thread = new Thread(RunMessageLoop)
        {
            Name = "HTWind.RuntimeTests.STA",
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = GetDispatcher();
        return dispatcher
            .InvokeAsync(
                async () => await action(),
                DispatcherPriority.Normal)
            .Task
            .Unwrap();
    }

    public Task RunAsync(Func<Task> action)
    {
        return RunAsync(
            async () =>
            {
                await action();
                return true;
            });
    }

    private Dispatcher GetDispatcher()
    {
        return _dispatcherReady.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
    }

    private void RunMessageLoop()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(dispatcher));
        _dispatcherReady.TrySetResult(dispatcher);
        Dispatcher.Run();
    }

    public void Dispose()
    {
        try
        {
            var dispatcher = _dispatcherReady.Task.Status == TaskStatus.RanToCompletion
                ? _dispatcherReady.Task.Result
                : null;
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Normal);
            _thread.Join(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // The thread may already be gone; shutdown is best effort.
        }
    }
}
