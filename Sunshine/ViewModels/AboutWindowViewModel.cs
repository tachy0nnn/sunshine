using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sunshine.ViewModels;

// page marker objects
public class AboutPage
{
}

public class ContributorsPage
{
}

public class LicensesPage
{
}

public partial class AboutWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAboutTab))]
    [NotifyPropertyChangedFor(nameof(IsContributorsTab))]
    [NotifyPropertyChangedFor(nameof(IsLicensesTab))]
    [NotifyPropertyChangedFor(nameof(AboutIconColor))]
    [NotifyPropertyChangedFor(nameof(ContributorsIconColor))]
    [NotifyPropertyChangedFor(nameof(LicensesIconColor))]
    private object _currentPage = new AboutPage();

    public bool IsAboutTab => CurrentPage is AboutPage;
    public bool IsContributorsTab => CurrentPage is ContributorsPage;
    public bool IsLicensesTab => CurrentPage is LicensesPage;

    public string AboutIconColor => IsAboutTab ? "#f0c040" : "#555";
    public string ContributorsIconColor => IsContributorsTab ? "#f0c040" : "#555";
    public string LicensesIconColor => IsLicensesTab ? "#f0c040" : "#555";

    [RelayCommand]
    private void ShowAbout()
    {
        CurrentPage = new AboutPage();
    }

    [RelayCommand]
    private void ShowContributors()
    {
        CurrentPage = new ContributorsPage();
    }

    [RelayCommand]
    private void ShowLicenses()
    {
        CurrentPage = new LicensesPage();
    }
}