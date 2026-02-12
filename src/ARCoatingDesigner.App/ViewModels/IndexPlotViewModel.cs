using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ARCoatingDesigner.Core.Catalogs;

namespace ARCoatingDesigner.App.ViewModels;

public partial class IndexPlotViewModel : ObservableObject
{
    private readonly CatalogService _catalogService;

    // Source type: 0 = Glass Catalog, 1 = Coating Material
    [ObservableProperty] private int _sourceTypeIndex;
    [ObservableProperty] private string? _selectedCatalog;
    [ObservableProperty] private string? _selectedGlass;
    [ObservableProperty] private string? _selectedMaterial;

    // Wavelength range
    [ObservableProperty] private double _wavelengthMin = 0.3;
    [ObservableProperty] private double _wavelengthMax = 2.0;
    [ObservableProperty] private double _wavelengthStep = 0.005;

    // Chart data
    [ObservableProperty] private double[] _chartWavelengths = Array.Empty<double>();
    [ObservableProperty] private double[] _chartN = Array.Empty<double>();
    [ObservableProperty] private double[] _chartK = Array.Empty<double>();
    [ObservableProperty] private string _statusText = "Ready";

    public ObservableCollection<string> AvailableCatalogs { get; } = new();
    public ObservableCollection<string> AvailableGlasses { get; } = new();
    public ObservableCollection<string> AvailableMaterials { get; } = new();

    public bool IsGlassSource => SourceTypeIndex == 0;
    public bool IsMaterialSource => SourceTypeIndex == 1;

    public event Action? ChartDataChanged;

    public IndexPlotViewModel(CatalogService catalogService, string? initialCatalog = null, string? initialGlass = null)
    {
        _catalogService = catalogService;

        // Populate glass catalogs
        foreach (var name in _catalogService.GlassCatalogNames)
            AvailableCatalogs.Add(name);

        // Use the main window's catalog, or fall back to SCHOTT
        if (initialCatalog != null && AvailableCatalogs.Contains(initialCatalog))
            SelectedCatalog = initialCatalog;
        else
        {
            var schott = AvailableCatalogs.FirstOrDefault(c => c.Contains("SCHOTT", StringComparison.OrdinalIgnoreCase));
            SelectedCatalog = schott ?? AvailableCatalogs.FirstOrDefault();
        }

        // Use the main window's glass if it's in the list
        if (initialGlass != null && AvailableGlasses.Contains(initialGlass))
            SelectedGlass = initialGlass;

        foreach (var name in _catalogService.CoatingMaterialNames)
            AvailableMaterials.Add(name);

        SelectedMaterial = AvailableMaterials.FirstOrDefault();

        // Auto-calculate on open
        Calculate();
    }

    partial void OnSourceTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGlassSource));
        OnPropertyChanged(nameof(IsMaterialSource));
        Calculate();
    }

    partial void OnSelectedCatalogChanged(string? value)
    {
        RefreshGlassList();
    }

    partial void OnSelectedGlassChanged(string? value)
    {
        if (IsGlassSource && value != null)
            Calculate();
    }

    partial void OnSelectedMaterialChanged(string? value)
    {
        if (IsMaterialSource && value != null)
            Calculate();
    }

    private void RefreshGlassList()
    {
        string? previousGlass = SelectedGlass;
        AvailableGlasses.Clear();

        if (!string.IsNullOrEmpty(SelectedCatalog))
        {
            foreach (var name in _catalogService.GetGlassNamesFromCatalog(SelectedCatalog))
                AvailableGlasses.Add(name);
        }

        if (previousGlass != null && AvailableGlasses.Contains(previousGlass))
            SelectedGlass = previousGlass;
        else
            SelectedGlass = AvailableGlasses.FirstOrDefault();
    }

    public void Calculate()
    {
        try
        {
            if (WavelengthMin >= WavelengthMax || WavelengthStep <= 0)
            {
                StatusText = "Invalid wavelength range";
                return;
            }

            int count = (int)((WavelengthMax - WavelengthMin) / WavelengthStep) + 1;
            var wavelengths = new double[count];
            var nValues = new double[count];
            var kValues = new double[count];

            string? name = IsGlassSource ? SelectedGlass : SelectedMaterial;
            if (string.IsNullOrEmpty(name))
            {
                StatusText = "No material selected";
                return;
            }

            for (int i = 0; i < count; i++)
            {
                double wl = WavelengthMin + i * WavelengthStep;
                wavelengths[i] = wl;

                if (IsGlassSource)
                {
                    nValues[i] = _catalogService.GetGlassIndex(name, wl);
                    kValues[i] = 0;
                }
                else
                {
                    var material = _catalogService.GetCoatingMaterial(name);
                    if (material == null)
                    {
                        StatusText = $"Material '{name}' not found";
                        return;
                    }
                    var (n, k) = material.GetNK(wl);
                    nValues[i] = n;
                    kValues[i] = k;
                }
            }

            ChartWavelengths = wavelengths;
            ChartN = nValues;
            ChartK = kValues;

            StatusText = $"Calculated {count} points for {name}";
            ChartDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
}
