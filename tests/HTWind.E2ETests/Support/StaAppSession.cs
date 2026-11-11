using System.IO;
using System.Windows;
using System.Windows.Threading;

using HTWind.E2ETests.Support;

namespace HTWind.E2ETests.Support;

/// <summary>
///     Launches the real WPF application in-process on a dedicated STA thread and drives a
///     scenario on its dispatcher. The scenario receives the running App; the harness reports
///     either its result or the first failure, whichever comes first.
/// </summary>
public static class StaAppSession
{
    public static AppRunResult Run(Func<Application, Task> scenario, TimeSpan? overallTimeout = null)
    {
        E2EPaths.Initialize();
        _scenarioMarkedComplete = false;

        var completion = new TaskCompletionSource<AppRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            App? app = null;
            try
            {
                app = new App();
                app.InitializeComponent();

                _ = app.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await scenario(app);
                        completion.TrySetResult(new AppRunResult(true, string.Empty));
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetResult(new AppRunResult(false, exception.ToString()));
                    }
                    finally
                    {
                        // End the scenario deterministically even if the app keeps running.
                        app.Shutdown(0);
                    }
                });

                app.Run();
                completion.TrySetResult(
                    completion.Task.Status == TaskStatus.RanToCompletion
                        ? completion.Task.GetAwaiter().GetResult()
                        : _scenarioMarkedComplete
                            ? new AppRunResult(true, string.Empty)
                            : new AppRunResult(false, "The application exited before the scenario completed."));
            }
            catch (Exception exception)
            {
                completion.TrySetResult(new AppRunResult(false, exception.ToString()));
            }
        })
        {
            Name = "HTWind.E2ETests.STA",
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var finished = completion.Task.WaitAsync(overallTimeout ?? TimeSpan.FromMinutes(3))
            .GetAwaiter().GetResult();
        return finished;
    }

    private static bool _scenarioMarkedComplete;

    /// <summary>
    ///     Marks the scenario as logically complete when the FINAL action is the application
    ///     exiting itself (e.g. tray Exit); the dispatcher is gone by the time the scenario
    ///     continuation would run.
    /// </summary>
    public static void MarkScenarioComplete()
    {
        _scenarioMarkedComplete = true;
    }

    public sealed record AppRunResult(bool Succeeded, string Failure);

    public static void RaiseClick(object handlerOwner, string handlerName)
    {
        var method = handlerOwner.GetType().GetMethod(
            handlerName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException($"Handler '{handlerName}' was not found on {handlerOwner.GetType().Name}.");
        }

        method.Invoke(handlerOwner, [handlerOwner, new RoutedEventArgs()]);
    }

    /// <summary>Polls a condition on the dispatcher without blocking its message loop.</summary>
    public static async Task WaitForAsync(
        Func<bool> condition,
        string description,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for: {description}");
    }
}
