using CommunityToolkit.Mvvm.Input;

namespace Sunshine.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    [RelayCommand]
    private void Launch()
    {
        /* launch logic */
    }

    [RelayCommand]
    private void Settings()
    {
        /* open settings */
    }

    [RelayCommand]
    private void LaunchStudio()
    {
        /* launch studio */
    }
}