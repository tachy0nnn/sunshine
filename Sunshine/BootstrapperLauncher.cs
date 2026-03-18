using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Sunshine.ViewModels;
using Sunshine.Views;

namespace Sunshine;

/// <summary>
///     Helper for Bootstrapper.cs; just... launches.
/// </summary>
public static class BootstrapperLauncher
{
    public static void Launch(LaunchMode mode)
    {
        Logger.WriteLine("BootstrapperLauncher::Launch", $"launching mode={mode}");

        var bootstrapper = new Bootstrapper(mode);
        var window = new BootstrapperWindow();

        var vm = new BootstrapperViewModel(bootstrapper, () =>
        {
            Logger.WriteLine("BootstrapperLauncher::Launch", "bootstrapper finished, closing window");
            window.Close();
        });

        window.DataContext = vm;

        // get the main window so we can show the dialog relative to it
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            window.ShowDialog(desktop.MainWindow!);
        else
            window.Show();

        Logger.WriteLine("BootstrapperLauncher::Launch", "window shown, starting async task");

        // start the actual work
        _ = vm.StartAsync();
    }
}