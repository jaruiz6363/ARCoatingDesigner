using Avalonia.Controls;
using Avalonia.Interactivity;
using ARCoatingDesigner.App.ViewModels;

namespace ARCoatingDesigner.App.Views;

public partial class GenerateMeritDialog : Window
{
    public GenerateMeritDialog()
    {
        InitializeComponent();
    }

    private void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GenerateMeritViewModel vm)
            vm.DialogResult = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GenerateMeritViewModel vm)
            vm.DialogResult = false;
        Close();
    }
}
