using Avalonia.Controls;
using Avalonia.Interactivity;
using ARCoatingDesigner.App.ViewModels;

namespace ARCoatingDesigner.App.Views;

public partial class AddMaterialDialog : Window
{
    public AddMaterialDialog()
    {
        InitializeComponent();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddMaterialViewModel vm) return;

        var material = vm.BuildMaterial();
        if (material == null)
            return; // Validation failed, error message is set

        vm.DialogResult = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AddMaterialViewModel vm)
            vm.DialogResult = false;
        Close();
    }
}
