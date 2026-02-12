using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ARCoatingDesigner.App.Views;

public partial class SaveToCatalogDialog : Window
{
    public string? CoatingName { get; private set; }

    public SaveToCatalogDialog()
    {
        InitializeComponent();
    }

    public SaveToCatalogDialog(string defaultName) : this()
    {
        NameBox.Text = defaultName;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        CoatingName = NameBox.Text?.Trim();
        if (!string.IsNullOrEmpty(CoatingName))
            Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
