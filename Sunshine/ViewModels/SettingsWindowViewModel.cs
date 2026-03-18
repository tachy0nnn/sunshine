using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sunshine.ViewModels;

// each page carries its own properties so DataTemplate bindings resolve correctly
public partial class BehaviourSettingsPage : ObservableObject
{
    [ObservableProperty] private bool _allowActivityJoining;
    [ObservableProperty] private bool _discordRichPresence = true;
    [ObservableProperty] private bool _enableActivityTracking = true;
    [ObservableProperty] private bool _showRobloxAccount;
}

public partial class AppearanceSettingsPage : ObservableObject
{
    [ObservableProperty] private bool _darkMode = true;
}

public class FastFlagsSettingsPage : ObservableObject
{
}

public partial class GlobalSettingsPage : ObservableObject
{
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _minimizeToTray = true;
}

public partial class DeploymentSettingsPage : ObservableObject
{
    [ObservableProperty] private bool _autoUpdate = true;
    [ObservableProperty] private string _channel = "production";
    [ObservableProperty] private bool _forceReinstall;
    [ObservableProperty] private bool _staticDirectory;

    // read-only info fields
    public string Version => "1.0.0";
    public string VersionGuid => "version-local";
    public string Timestamp => DateTime.Now.ToString("dd.MM.yyyy HH:mm");
}

public partial class SettingsWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBehaviourTab))]
    [NotifyPropertyChangedFor(nameof(IsAppearanceTab))]
    [NotifyPropertyChangedFor(nameof(IsFastFlagsTab))]
    [NotifyPropertyChangedFor(nameof(IsGlobalTab))]
    [NotifyPropertyChangedFor(nameof(BehaviourIconColor))]
    [NotifyPropertyChangedFor(nameof(AppearanceIconColor))]
    [NotifyPropertyChangedFor(nameof(FastFlagsIconColor))]
    [NotifyPropertyChangedFor(nameof(GlobalIconColor))]
    [NotifyPropertyChangedFor(nameof(IsDeploymentTab))]
    [NotifyPropertyChangedFor(nameof(DeploymentIconColor))]
    private object _currentPage = new BehaviourSettingsPage();

    // notification banner state
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NotificationVisible))]
    private string _notificationMessage = "";

    public bool IsBehaviourTab => CurrentPage is BehaviourSettingsPage;
    public bool IsAppearanceTab => CurrentPage is AppearanceSettingsPage;
    public bool IsFastFlagsTab => CurrentPage is FastFlagsSettingsPage;
    public bool IsGlobalTab => CurrentPage is GlobalSettingsPage;
    public bool IsDeploymentTab => CurrentPage is DeploymentSettingsPage;


    public string BehaviourIconColor => IsBehaviourTab ? "#f0c040" : "#555";
    public string AppearanceIconColor => IsAppearanceTab ? "#f0c040" : "#555";
    public string FastFlagsIconColor => IsFastFlagsTab ? "#f0c040" : "#555";
    public string GlobalIconColor => IsGlobalTab ? "#f0c040" : "#555";
    public string DeploymentIconColor => IsDeploymentTab ? "#f0c040" : "#555";

    public bool NotificationVisible => !string.IsNullOrEmpty(NotificationMessage);

    [RelayCommand]
    private void ShowBehaviour()
    {
        CurrentPage = new BehaviourSettingsPage();
    }

    [RelayCommand]
    private void ShowAppearance()
    {
        CurrentPage = new AppearanceSettingsPage();
    }

    [RelayCommand]
    private void ShowFastFlags()
    {
        CurrentPage = new FastFlagsSettingsPage();
    }

    [RelayCommand]
    private void ShowGlobal()
    {
        CurrentPage = new GlobalSettingsPage();
    }

    [RelayCommand]
    private void ShowDeployment()
    {
        CurrentPage = new DeploymentSettingsPage();
    }

    [RelayCommand]
    private void Save()
    {
        // save logic here
        NotificationMessage = "Settings saved!";
    }

    [RelayCommand]
    private void SaveAndLaunch()
    {
        // save + launch logic here
        NotificationMessage = "Settings saved!";
    }
}