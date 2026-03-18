using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sunshine.ViewModels;

namespace Sunshine.Views;

public partial class BootstrapperWindow : Window
{
    public BootstrapperWindow()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BootstrapperViewModel vm)
            vm.Cancel();
    }
}