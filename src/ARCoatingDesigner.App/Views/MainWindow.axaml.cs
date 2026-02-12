using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ScottPlot;
using ARCoatingDesigner.App.ViewModels;

namespace ARCoatingDesigner.App.Views;

public partial class MainWindow : Window
{
    private bool _isHandlingOpticalThickness;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;
        if (DataContext is not MainViewModel vm) return;
        if (!vm.CatalogDirty) return;

        e.Cancel = true;

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Unsaved Catalog Changes",
            "The coating catalog has unsaved changes.\n\nDo you want to save before closing?",
            ButtonEnum.YesNoCancel,
            MsBox.Avalonia.Enums.Icon.Warning);

        var result = await box.ShowWindowDialogAsync(this);

        if (result == ButtonResult.Cancel)
            return;

        if (result == ButtonResult.Yes)
            vm.SaveCatalog();

        _forceClose = true;
        Close();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            vm.ChartDataChanged += UpdatePlot;
            // Initial plot
            UpdatePlot();
        }
    }

    private void UpdatePlot()
    {
        if (DataContext is not MainViewModel vm) return;

        var plt = AvaPlot1.Plot;
        plt.Clear();

        if (vm.ChartX.Length == 0) return;

        var sigRs = plt.Add.ScatterLine(vm.ChartX, vm.ChartRs);
        sigRs.Color = ScottPlot.Color.FromHex("#0000FF");
        sigRs.LineWidth = 1.5f;
        sigRs.LegendText = "Rs";

        var sigRp = plt.Add.ScatterLine(vm.ChartX, vm.ChartRp);
        sigRp.Color = ScottPlot.Color.FromHex("#FF0000");
        sigRp.LineWidth = 1.5f;
        sigRp.LegendText = "Rp";

        var sigRave = plt.Add.ScatterLine(vm.ChartX, vm.ChartRave);
        sigRave.Color = ScottPlot.Color.FromHex("#008000");
        sigRave.LineWidth = 2.5f;
        sigRave.LegendText = "Rave";

        if (vm.PlotVsWavelength)
        {
            plt.XLabel("Wavelength (μm)");
            plt.YLabel("Reflectance (%)");
            plt.Title($"Reflectance vs Wavelength (AOI = {vm.PlotAOI:F1}°)");
            plt.Axes.SetLimits(vm.PlotWavelengthMin, vm.PlotWavelengthMax, vm.PlotYMin, vm.PlotYMax);
        }
        else
        {
            plt.XLabel("Angle of Incidence (°)");
            plt.YLabel("Reflectance (%)");
            plt.Title($"Reflectance vs Angle (λ = {vm.PlotWavelengthForAngle * 1000:F0} nm)");
            plt.Axes.SetLimits(vm.PlotAngleMin, vm.PlotAngleMax, vm.PlotYMin, vm.PlotYMax);
        }
        plt.ShowLegend(Alignment.UpperRight);

        AvaPlot1.Refresh();
    }

    private async void GenerateTargetsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dialogVm = new GenerateMeritViewModel
        {
            AoiMin = vm.PlotAOI,
            AoiMax = vm.PlotAOI
        };

        var dlg = new GenerateMeritDialog { DataContext = dialogVm };
        await dlg.ShowDialog(this);

        if (dialogVm.DialogResult)
        {
            vm.GenerateTargetsFromDialog(dialogVm);
        }
    }

    private void RefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.UpdateChart();
    }

    private void PlotVsWavelengthClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PlotVsWavelength = true;
    }

    private void PlotVsAngleClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PlotVsWavelength = false;
    }

    private async void OpenCatalogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Coating Catalog",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Coating Catalog") { Patterns = new[] { "*.coat" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            vm.OpenCatalog(files[0].Path.LocalPath);
        }
    }

    private void SaveCatalogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveCatalog();
    }

    private async void SaveCatalogAsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Catalog As",
            DefaultExtension = "coat",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Coating Catalog") { Patterns = new[] { "*.coat" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            vm.SaveCatalogAs(file.Path.LocalPath);
        }
    }

    private async void LoadDesignFromCatalogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var coatingNames = vm.GetCatalogCoatingNames();
        if (coatingNames.Length == 0)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Empty Catalog",
                "No coatings found in the catalog.\nUse Catalog > Open Catalog to load a .coat file.",
                ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
            await box.ShowWindowDialogAsync(this);
            return;
        }

        var dlg = new CatalogSelectDialog(coatingNames, "Load Design from Catalog");
        var result = await dlg.ShowDialog<bool?>(this);
        if (result == true && !string.IsNullOrEmpty(dlg.SelectedCoating))
        {
            vm.LoadFromCatalog(dlg.SelectedCoating);
        }
    }

    private async void SaveDesignToCatalogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dlg = new SaveToCatalogDialog(vm.CoatingName);
        var result = await dlg.ShowDialog<bool?>(this);
        if (result == true && !string.IsNullOrEmpty(dlg.CoatingName))
        {
            vm.SaveToCatalog(dlg.CoatingName);
        }
    }

    private async void ExportTextClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Text",
            DefaultExtension = "txt",
            SuggestedFileName = vm.CoatingName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
            vm.ExportText(file.Path.LocalPath);
    }

    private async void ExportZemaxClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export ZEMAX Coating",
            DefaultExtension = "dat",
            SuggestedFileName = vm.CatalogFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("ZEMAX Coating") { Patterns = new[] { "*.dat" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
            vm.ExportToZemaxDat(file.Path.LocalPath);
    }

    private async void AddMaterialClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dialogVm = new AddMaterialViewModel();
        var dlg = new AddMaterialDialog { DataContext = dialogVm };
        await dlg.ShowDialog(this);

        if (dialogVm.DialogResult)
        {
            var material = dialogVm.BuildMaterial();
            if (material != null)
                vm.AddCoatingMaterial(material);
        }
    }

    private async void IndexPlotClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dialogVm = new IndexPlotViewModel(vm.CatalogService, vm.SelectedCatalog, vm.SubstrateMaterial);
        var dlg = new IndexPlotDialog { DataContext = dialogVm };
        await dlg.ShowDialog(this);
    }

    private void StopOptimizationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.StopOptimization();
    }

    private async void OnOpticalThicknessClick(object? sender, RoutedEventArgs e)
    {
        if (_isHandlingOpticalThickness) return;
        if (DataContext is not MainViewModel vm) return;

        _isHandlingOpticalThickness = true;

        try
        {
            bool convertingToOptical = vm.UseOpticalThickness;
            bool hasLayers = vm.Layers.Count > 0;

            if (hasLayers)
            {
                string fromType = convertingToOptical ? "physical (um)" : "optical (waves)";
                string toType = convertingToOptical ? "optical (waves)" : "physical (um)";

                var box = MessageBoxManager.GetMessageBoxStandard(
                    "Convert Thicknesses?",
                    $"Do you want to convert existing layer thicknesses from {fromType} to {toType}?\n\n" +
                    $"Reference wavelength: {vm.ReferenceWavelength} um\n\n" +
                    "Click Yes to convert, No to keep values as-is.",
                    ButtonEnum.YesNoCancel,
                    MsBox.Avalonia.Enums.Icon.Question);

                var result = await box.ShowWindowDialogAsync(this);

                if (result == ButtonResult.Cancel)
                {
                    vm.RevertOpticalThickness();
                    return;
                }

                vm.SetOpticalThickness(convertingToOptical, result == ButtonResult.Yes);
            }
        }
        finally
        {
            _isHandlingOpticalThickness = false;
        }
    }
}
