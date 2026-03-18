using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Sunshine.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    [RelayCommand]
    private async Task Launch()
    {
        await LaunchBootstrapper(LaunchMode.Player);
    }

    [RelayCommand]
    private async Task LaunchStudio()
    {
        await LaunchBootstrapper(LaunchMode.Studio);
    }

    private static Task LaunchBootstrapper(LaunchMode mode)
    {
        BootstrapperLauncher.Launch(mode);
        return Task.CompletedTask;
    }
}