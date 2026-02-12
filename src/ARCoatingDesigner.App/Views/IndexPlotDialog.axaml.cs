using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScottPlot;
using ARCoatingDesigner.App.ViewModels;

namespace ARCoatingDesigner.App.Views;

public partial class IndexPlotDialog : Window
{
    public IndexPlotDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is IndexPlotViewModel vm)
        {
            vm.ChartDataChanged += UpdatePlot;
            // Render data that was calculated during construction
            if (vm.ChartWavelengths.Length > 0)
                UpdatePlot();
        }
    }

    private void UpdatePlot()
    {
        if (DataContext is not IndexPlotViewModel vm) return;

        var plt = AvaPlot1.Plot;
        plt.Clear();

        if (vm.ChartWavelengths.Length == 0) return;

        var sigN = plt.Add.ScatterLine(vm.ChartWavelengths, vm.ChartN);
        sigN.Color = ScottPlot.Color.FromHex("#0000FF");
        sigN.LineWidth = 2f;
        sigN.LegendText = "n";

        bool hasK = false;
        for (int i = 0; i < vm.ChartK.Length; i++)
        {
            if (vm.ChartK[i] != 0) { hasK = true; break; }
        }

        if (hasK)
        {
            var sigK = plt.Add.ScatterLine(vm.ChartWavelengths, vm.ChartK);
            sigK.Color = ScottPlot.Color.FromHex("#FF0000");
            sigK.LineWidth = 2f;
            sigK.LegendText = "k";
            sigK.Axes.YAxis = plt.Axes.Right;
            plt.Axes.Right.Label.Text = "k (extinction)";
        }

        string name = vm.IsGlassSource ? vm.SelectedGlass ?? "" : vm.SelectedMaterial ?? "";
        plt.XLabel("Wavelength (μm)");
        plt.YLabel("n (refractive index)");
        plt.Title($"Index of Refraction — {name}");

        // Set axis limits from data
        double xMin = vm.ChartWavelengths.Min();
        double xMax = vm.ChartWavelengths.Max();
        double nMin = vm.ChartN.Min();
        double nMax = vm.ChartN.Max();
        double nPad = Math.Max((nMax - nMin) * 0.05, 0.01);
        plt.Axes.SetLimits(xMin, xMax, nMin - nPad, nMax + nPad);

        if (hasK)
        {
            double kMin = vm.ChartK.Min();
            double kMax = vm.ChartK.Max();
            double kPad = Math.Max((kMax - kMin) * 0.05, 0.001);
            plt.Axes.Right.Min = kMin - kPad;
            plt.Axes.Right.Max = kMax + kPad;
        }

        plt.ShowLegend(Alignment.UpperRight);

        AvaPlot1.Refresh();
    }

    private void OnCalculateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IndexPlotViewModel vm)
            vm.Calculate();
    }

    private async void OnExportTextClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IndexPlotViewModel vm) return;
        if (vm.ChartWavelengths.Length == 0)
        {
            vm.StatusText = "No data to export. Click Calculate first.";
            return;
        }

        string suggestedName = vm.IsGlassSource ? vm.SelectedGlass ?? "index" : vm.SelectedMaterial ?? "index";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Index Data",
            DefaultExtension = "txt",
            SuggestedFileName = suggestedName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file == null) return;

        try
        {
            using var writer = new StreamWriter(file.Path.LocalPath);
            writer.WriteLine($"# Index of Refraction Data");
            writer.WriteLine($"# Material: {suggestedName}");
            writer.WriteLine($"# Source: {(vm.IsGlassSource ? "Glass Catalog" : "Coating Material")}");
            writer.WriteLine($"# Wavelength range: {vm.WavelengthMin:F3} - {vm.WavelengthMax:F3} μm, step {vm.WavelengthStep:F3}");
            writer.WriteLine();
            writer.WriteLine("Wavelength_um\tn\tk");

            for (int i = 0; i < vm.ChartWavelengths.Length; i++)
                writer.WriteLine($"{vm.ChartWavelengths[i]:F4}\t{vm.ChartN[i]:F6}\t{vm.ChartK[i]:F6}");

            vm.StatusText = $"Exported to {Path.GetFileName(file.Path.LocalPath)}";
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Export error: {ex.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
