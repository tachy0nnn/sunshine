using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sunshine.ViewModels;

public partial class BootstrapperViewModel : ViewModelBase
{
    private readonly Bootstrapper _bootstrapper;
    private readonly Action _onFinished;
    [ObservableProperty] private bool _canCancel = true;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private double _progress;

    [ObservableProperty] private string _statusText = "Preparing…";

    public BootstrapperViewModel(Bootstrapper bootstrapper, Action onFinished)
    {
        _bootstrapper = bootstrapper;
        _onFinished = onFinished;

        bootstrapper.StatusChanged += s => Dispatcher.UIThread.Post(() =>
        {
            StatusText = s;
            IsIndeterminate = true;
        });

        bootstrapper.ProgressChanged += p => Dispatcher.UIThread.Post(() =>
        {
            Progress = p;
            IsIndeterminate = p <= 0;
        });
    }

    public void Cancel()
    {
        Logger.WriteLine("BootstrapperViewModel::Cancel", "user requested cancel");
        _bootstrapper.Cancel();
        CanCancel = false;
        StatusText = "Cancelling…";
    }

    /// <summary>
    ///     starts the bootstrapper task and invokes onFinished (on the ui thread) when done.
    /// </summary>
    public async Task StartAsync()
    {
        Logger.WriteLine("BootstrapperViewModel::StartAsync", "starting bootstrapper task");
        try
        {
            await _bootstrapper.RunAsync();
            Logger.WriteLine("BootstrapperViewModel::StartAsync", "bootstrapper task completed successfully");
        }
        catch (OperationCanceledException)
        {
            Logger.WriteLine("BootstrapperViewModel::StartAsync", "task cancelled by user");
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            Logger.WriteException("BootstrapperViewModel::StartAsync", ex);
            StatusText = $"Error: {ex.Message}";
            // give the user a moment to read the error before the window closes
            await Task.Delay(3000);
        }
        finally
        {
            Logger.WriteLine("BootstrapperViewModel::StartAsync", "invoking onFinished callback");
            Dispatcher.UIThread.Post(_onFinished);
        }
    }
}