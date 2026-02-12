using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ARCoatingDesigner.App.Views;

public partial class CatalogSelectDialog : Window
{
    public string? SelectedCoating { get; private set; }

    public CatalogSelectDialog()
    {
        InitializeComponent();
    }

    public CatalogSelectDialog(string[] coatingNames, string title = "Select Coating") : this()
    {
        Title = title;
        CoatingList.ItemsSource = coatingNames;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = CoatingList.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        SelectedCoating = CoatingList.SelectedItem as string;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
