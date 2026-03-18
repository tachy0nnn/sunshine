using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sunshine.ViewModels;

namespace Sunshine.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow { DataContext = new AboutWindowViewModel() };
        await dialog.ShowDialog(this);
    }

    private async void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow { DataContext = new SettingsWindowViewModel() };
        await dialog.ShowDialog(this);
    }
}