using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sunshine.ViewModels;

public class BehaviourSettingsPage(SunshineSettings settings) : ObservableObject
{
    public bool EnableActivityTracking
    {
        get => settings.EnableActivityTracking;
        set
        {
            settings.EnableActivityTracking = value;
            OnPropertyChanged();

            // disabling activity tracking will disable RPC
            if (!value)
            {
                DiscordRichPresence = false;
                AllowActivityJoining = false;
                ShowRobloxAccount = false;
            }
        }
    }

    public bool DiscordRichPresence
    {
        get => settings.DiscordRichPresence;
        set
        {
            settings.DiscordRichPresence = value;
            OnPropertyChanged();
        }
    }

    public bool AllowActivityJoining
    {
        get => settings.AllowActivityJoining;
        set
        {
            settings.AllowActivityJoining = value;
            OnPropertyChanged();
        }
    }

    public bool ShowRobloxAccount
    {
        get => settings.ShowRobloxAccount;
        set
        {
            settings.ShowRobloxAccount = value;
            OnPropertyChanged();
        }
    }
}

public class AppearanceSettingsPage(SunshineSettings settings) : ObservableObject
{
    public bool DarkMode
    {
        get => settings.DarkMode;
        set
        {
            settings.DarkMode = value;
            OnPropertyChanged();
        }
    }
}

public class FastFlagsSettingsPage : ObservableObject
{
}

public class GlobalSettingsPage(SunshineSettings settings) : ObservableObject
{
    public bool LaunchOnStartup
    {
        get => settings.LaunchOnStartup;
        set
        {
            settings.LaunchOnStartup = value;
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTray
    {
        get => settings.MinimizeToTray;
        set
        {
            settings.MinimizeToTray = value;
            OnPropertyChanged();
        }
    }
}

public class DeploymentSettingsPage(SunshineSettings settings) : ObservableObject
{
    public bool AutoUpdate
    {
        get => settings.AutoUpdate;
        set
        {
            settings.AutoUpdate = value;
            OnPropertyChanged();
        }
    }

    public string Channel
    {
        get => settings.Channel;
        set
        {
            settings.Channel = value;
            OnPropertyChanged();
        }
    }

    public bool ForceReinstall
    {
        get => settings.ForceReinstall;
        set
        {
            settings.ForceReinstall = value;
            OnPropertyChanged();
        }
    }

    public bool StaticDirectory
    {
        get => settings.StaticDirectory;
        set
        {
            settings.StaticDirectory = value;
            OnPropertyChanged();
        }
    }

    public string Version => "1.0.0";
    public string VersionGuid => "version-local";
    public string Timestamp => DateTime.Now.ToString("dd.MM.yyyy HH:mm");
}

public partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly SunshineSettings _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBehaviourTab))]
    [NotifyPropertyChangedFor(nameof(IsAppearanceTab))]
    [NotifyPropertyChangedFor(nameof(IsFastFlagsTab))]
    [NotifyPropertyChangedFor(nameof(IsGlobalTab))]
    [NotifyPropertyChangedFor(nameof(IsDeploymentTab))]
    [NotifyPropertyChangedFor(nameof(BehaviourIconColor))]
    [NotifyPropertyChangedFor(nameof(AppearanceIconColor))]
    [NotifyPropertyChangedFor(nameof(FastFlagsIconColor))]
    [NotifyPropertyChangedFor(nameof(GlobalIconColor))]
    [NotifyPropertyChangedFor(nameof(DeploymentIconColor))]
    private object _currentPage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NotificationVisible))]
    private string _notificationMessage = "";

    public SettingsWindowViewModel()
    {
        _settings = SunshineSettings.Load();
        _currentPage = new BehaviourSettingsPage(_settings);
    }

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
        CurrentPage = new BehaviourSettingsPage(_settings);
    }

    [RelayCommand]
    private void ShowAppearance()
    {
        CurrentPage = new AppearanceSettingsPage(_settings);
    }

    [RelayCommand]
    private void ShowFastFlags()
    {
        CurrentPage = new FastFlagsSettingsPage();
    }

    [RelayCommand]
    private void ShowGlobal()
    {
        CurrentPage = new GlobalSettingsPage(_settings);
    }

    [RelayCommand]
    private void ShowDeployment()
    {
        CurrentPage = new DeploymentSettingsPage(_settings);
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Save();
        NotificationMessage = "Settings saved!";
        Logger.WriteLine("SettingsWindowViewModel::Save", "settings saved");
    }

    [RelayCommand]
    private void SaveAndLaunch()
    {
        _settings.Save();
        NotificationMessage = "Settings saved!";
        Logger.WriteLine("SettingsWindowViewModel::SaveAndLaunch", "settings saved, launching");
        // bootstrapper launch is triggered by the window via BootstrapperLauncher
    }
}