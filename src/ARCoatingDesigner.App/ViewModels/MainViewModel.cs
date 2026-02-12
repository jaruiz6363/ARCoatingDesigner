using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ARCoatingDesigner.Core.Calculations;
using ARCoatingDesigner.Core.Catalogs;
using ARCoatingDesigner.Core.Models;
using ARCoatingDesigner.Core.Optimization;

namespace ARCoatingDesigner.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CatalogService _catalogService;
    private readonly ThinFilmCalculator _calculator;
    private readonly CoatingOptimizer _optimizer;
    private CoatingDesign _design;

    public CatalogService CatalogService => _catalogService;

    public string CatalogFileName =>
        !string.IsNullOrEmpty(_catalogService.CoatingCatalogPath)
            ? Path.GetFileNameWithoutExtension(_catalogService.CoatingCatalogPath)
            : "CoatingExport";

    // Design properties
    [ObservableProperty] private string _coatingName = "SLAR";
    [ObservableProperty] private string _substrateMaterial = "N-BK7";
    [ObservableProperty] private double _referenceWavelength = 0.55;
    [ObservableProperty] private bool _useOpticalThickness = true;

    // Plot controls
    [ObservableProperty] private double _plotWavelengthMin = 0.4;
    [ObservableProperty] private double _plotWavelengthMax = 0.8;
    [ObservableProperty] private double _plotAOI = 0;
    [ObservableProperty] private double _plotYMin = 0;
    [ObservableProperty] private double _plotYMax = 10;
    [ObservableProperty] private bool _plotVsWavelength = true;
    [ObservableProperty] private double _plotAngleMin = 0;
    [ObservableProperty] private double _plotAngleMax = 80;
    [ObservableProperty] private double _plotWavelengthForAngle = 0.55;

    // Chart data
    [ObservableProperty] private double[] _chartX = Array.Empty<double>();
    [ObservableProperty] private double[] _chartRs = Array.Empty<double>();
    [ObservableProperty] private double[] _chartRp = Array.Empty<double>();
    [ObservableProperty] private double[] _chartRave = Array.Empty<double>();

    // Status
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private double _meritValue;
    [ObservableProperty] private bool _isOptimizing;
    public bool CatalogDirty { get; private set; }

    // Optimization settings
    [ObservableProperty] private int _optMaxIterations = 200;
    [ObservableProperty] private int _optNumTrials = 1000;
    [ObservableProperty] private double _optBestMerit;
    private CancellationTokenSource? _optimizationCts;

    // Collections
    public ObservableCollection<DesignLayerViewModel> Layers { get; } = new();
    public ObservableCollection<MeritTargetViewModel> Targets { get; } = new();
    public ObservableCollection<string> AvailableMaterials { get; } = new();
    public ObservableCollection<string> AvailableCatalogs { get; } = new();
    public ObservableCollection<string> AvailableGlasses { get; } = new();

    [ObservableProperty] private string? _selectedCatalog;
    [ObservableProperty] private DesignLayerViewModel? _selectedLayer;
    [ObservableProperty] private MeritTargetViewModel? _selectedTarget;

    // Chart update event for view to subscribe to
    public event Action? ChartDataChanged;

    public MainViewModel()
    {
        _catalogService = new CatalogService();
        _catalogService.InitializeWithStandardMaterials();
        _catalogService.TryLoadDefaultCatalogs();

        _calculator = new ThinFilmCalculator(_catalogService);
        _optimizer = new CoatingOptimizer(_calculator);

        // Populate available materials
        foreach (var name in _catalogService.CoatingMaterialNames)
            AvailableMaterials.Add(name);

        if (AvailableMaterials.Count == 0)
        {
            AvailableMaterials.Add("MgF2");
            AvailableMaterials.Add("SiO2");
            AvailableMaterials.Add("Al2O3");
            AvailableMaterials.Add("TiO2");
            AvailableMaterials.Add("Ta2O5");
            AvailableMaterials.Add("ZrO2");
            AvailableMaterials.Add("HfO2");
        }

        // Populate available catalogs
        AvailableCatalogs.Add("(All)");
        foreach (var name in _catalogService.GlassCatalogNames)
            AvailableCatalogs.Add(name);

        // Populate available glasses
        foreach (var name in _catalogService.GlassNames.Take(500))
            AvailableGlasses.Add(name);
        if (!AvailableGlasses.Contains("N-BK7"))
            AvailableGlasses.Insert(0, "N-BK7");

        // Default to SCHOTT if available, otherwise first catalog
        var schott = AvailableCatalogs.FirstOrDefault(c => c.Contains("SCHOTT", StringComparison.OrdinalIgnoreCase));
        SelectedCatalog = schott ?? AvailableCatalogs.FirstOrDefault();

        // Load default design
        _design = CoatingDesign.CreateDefaultSingleLayer();
        SyncFromDesign();
        UpdateChart();

        var glassCatalogInfo = _catalogService.GlassCatalogPath != null
            ? $"Glass: {Path.GetFileName(_catalogService.GlassCatalogPath)} ({_catalogService.GlassNames.Count()})"
            : "Glass: built-in defaults";
        StatusText = $"Ready | {glassCatalogInfo}";
    }

    private void RefreshGlassList()
    {
        string? previousSubstrate = SubstrateMaterial;
        AvailableGlasses.Clear();

        IEnumerable<string> glasses;
        if (string.IsNullOrEmpty(SelectedCatalog) || SelectedCatalog == "(All)")
            glasses = _catalogService.GlassNames.Take(500);
        else
            glasses = _catalogService.GetGlassNamesFromCatalog(SelectedCatalog);

        foreach (var name in glasses)
            AvailableGlasses.Add(name);

        if (!AvailableGlasses.Contains("N-BK7"))
            AvailableGlasses.Insert(0, "N-BK7");

        // Restore previous selection if it's still in the list, otherwise pick first
        if (previousSubstrate != null && AvailableGlasses.Contains(previousSubstrate))
            SubstrateMaterial = previousSubstrate;
        else if (AvailableGlasses.Count > 0)
            SubstrateMaterial = AvailableGlasses[0];
    }

    private void SyncFromDesign()
    {
        _isSyncingFromDesign = true;
        try
        {
            CoatingName = _design.Name;
            SubstrateMaterial = _design.SubstrateMaterial;
            ReferenceWavelength = _design.ReferenceWavelength_um;
            UseOpticalThickness = _design.UseOpticalThickness;

            Layers.Clear();
            foreach (var layer in _design.Layers)
            {
                var vm = new DesignLayerViewModel(layer);
                vm.LayerChanged += () => UpdateChart();
                Layers.Add(vm);
            }

            Targets.Clear();
            foreach (var target in _design.MeritTargets)
                Targets.Add(new MeritTargetViewModel(target));
        }
        finally
        {
            _isSyncingFromDesign = false;
        }
    }

    private void SyncToDesign()
    {
        if (_isSyncingFromDesign) return;
        _design.Name = CoatingName;
        _design.SubstrateMaterial = SubstrateMaterial;
        _design.ReferenceWavelength_um = ReferenceWavelength;
        _design.UseOpticalThickness = UseOpticalThickness;
    }

    partial void OnCoatingNameChanged(string value) { SyncToDesign(); }
    partial void OnSubstrateMaterialChanged(string value) { if (value != null) { SyncToDesign(); if (!_isSyncingFromDesign) UpdateChart(); } }
    partial void OnSelectedCatalogChanged(string? value) { RefreshGlassList(); }
    partial void OnReferenceWavelengthChanged(double value) { SyncToDesign(); if (!_isSyncingFromDesign) UpdateChart(); }
    partial void OnUseOpticalThicknessChanged(bool value)
    {
        if (!_suppressOpticalThicknessSync && !_isSyncingFromDesign)
        {
            SyncToDesign();
            UpdateChart();
        }
    }

    private bool _suppressOpticalThicknessSync;
    private bool _isSyncingFromDesign;

    /// <summary>
    /// Set optical thickness with optional conversion, called from code-behind after dialog.
    /// </summary>
    public void SetOpticalThickness(bool useOptical, bool convertThicknesses)
    {
        _suppressOpticalThicknessSync = true;
        UseOpticalThickness = useOptical;
        _suppressOpticalThicknessSync = false;

        if (convertThicknesses)
            ConvertLayerThicknesses(useOptical);

        SyncToDesign();
        for (int i = 0; i < Layers.Count; i++)
            Layers[i].NotifyThicknessChanged();
        UpdateChart();
    }

    /// <summary>
    /// Revert optical thickness checkbox without triggering conversion.
    /// </summary>
    public void RevertOpticalThickness()
    {
        _suppressOpticalThicknessSync = true;
        UseOpticalThickness = !UseOpticalThickness;
        _suppressOpticalThicknessSync = false;
    }

    private void ConvertLayerThicknesses(bool toOptical)
    {
        double refWavelength = _design.ReferenceWavelength_um;

        foreach (var layer in _design.Layers)
        {
            var material = _catalogService.GetCoatingMaterial(layer.MaterialName);
            double n = 1.5;
            if (material != null)
            {
                var (nVal, _) = material.GetNK(refWavelength);
                n = nVal;
            }

            if (toOptical)
            {
                // physical (um) -> optical (waves): optical = n * physical / wavelength
                layer.Thickness = n * layer.Thickness / refWavelength;
                layer.MinThickness = n * layer.MinThickness / refWavelength;
                layer.MaxThickness = n * layer.MaxThickness / refWavelength;
            }
            else
            {
                // optical (waves) -> physical (um): physical = optical * wavelength / n
                layer.Thickness = layer.Thickness * refWavelength / n;
                layer.MinThickness = layer.MinThickness * refWavelength / n;
                layer.MaxThickness = layer.MaxThickness * refWavelength / n;
            }
        }
    }

    partial void OnPlotWavelengthMinChanged(double value) => UpdateChart();
    partial void OnPlotWavelengthMaxChanged(double value) => UpdateChart();
    partial void OnPlotAOIChanged(double value) => UpdateChart();
    partial void OnPlotYMinChanged(double value) => ChartDataChanged?.Invoke();
    partial void OnPlotYMaxChanged(double value) => ChartDataChanged?.Invoke();
    partial void OnPlotVsWavelengthChanged(bool value) => UpdateChart();
    partial void OnPlotAngleMinChanged(double value) => UpdateChart();
    partial void OnPlotAngleMaxChanged(double value) => UpdateChart();
    partial void OnPlotWavelengthForAngleChanged(double value) => UpdateChart();

    public void UpdateChart()
    {
        try
        {
            SyncToDesign();

            if (PlotVsWavelength)
            {
                var (wavelengths, rs, rp, rave) = _calculator.CalculateSpectrum(
                    _design, PlotWavelengthMin, PlotWavelengthMax, 201, PlotAOI);

                ChartX = wavelengths;
                ChartRs = rs;
                ChartRp = rp;
                ChartRave = rave;
            }
            else
            {
                var (angles, rs, rp, rave) = _calculator.CalculateAngularResponse(
                    _design, PlotWavelengthForAngle, PlotAngleMin, PlotAngleMax, 201);

                ChartX = angles;
                ChartRs = rs;
                ChartRp = rp;
                ChartRave = rave;
            }

            // Update merit
            MeritValue = _calculator.CalculateMerit(_design);

            // Update calculated values on targets
            foreach (var target in Targets)
                target.CalculatedValue = _calculator.GetTargetValue(_design, target.Model);

            ChartDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    // Layer commands
    [RelayCommand]
    private void AddLayer()
    {
        string material = AvailableMaterials.FirstOrDefault() ?? "MgF2";
        double thickness = UseOpticalThickness ? 0.25 : 0.1;
        _design.AddLayer(material, thickness);
        var vm = new DesignLayerViewModel(_design.Layers[^1]);
        vm.LayerChanged += () => UpdateChart();
        Layers.Add(vm);
        UpdateChart();
    }

    [RelayCommand]
    private void RemoveLayer()
    {
        if (SelectedLayer == null) return;
        int index = Layers.IndexOf(SelectedLayer);
        if (index >= 0)
        {
            _design.RemoveLayerAt(index);
            Layers.RemoveAt(index);
            UpdateChart();
        }
    }

    [RelayCommand]
    private void MoveLayerUp()
    {
        if (SelectedLayer == null) return;
        int index = Layers.IndexOf(SelectedLayer);
        if (index > 0)
        {
            _design.MoveLayerUp(index);
            var item = Layers[index];
            Layers.RemoveAt(index);
            Layers.Insert(index - 1, item);
            SelectedLayer = item;
            UpdateChart();
        }
    }

    [RelayCommand]
    private void MoveLayerDown()
    {
        if (SelectedLayer == null) return;
        int index = Layers.IndexOf(SelectedLayer);
        if (index >= 0 && index < Layers.Count - 1)
        {
            _design.MoveLayerDown(index);
            var item = Layers[index];
            Layers.RemoveAt(index);
            Layers.Insert(index + 1, item);
            SelectedLayer = item;
            UpdateChart();
        }
    }

    // Notify chart when layer property changes
    public void OnLayerPropertyChanged()
    {
        UpdateChart();
    }

    // Target commands
    [RelayCommand]
    private void AddTarget()
    {
        _design.AddTarget(MeritTargetType.Rave, ReferenceWavelength, 0, 0, CompareType.Equal);
        Targets.Add(new MeritTargetViewModel(_design.MeritTargets[^1]));
        UpdateChart();
    }

    [RelayCommand]
    private void RemoveTarget()
    {
        if (SelectedTarget == null) return;
        int index = Targets.IndexOf(SelectedTarget);
        if (index >= 0)
        {
            _design.RemoveTargetAt(index);
            Targets.RemoveAt(index);
            UpdateChart();
        }
    }

    [RelayCommand]
    private void ClearTargets()
    {
        _design.MeritTargets.Clear();
        Targets.Clear();
        UpdateChart();
    }

    public void GenerateTargetsFromDialog(GenerateMeritViewModel settings)
    {
        _design.MeritTargets.Clear();
        Targets.Clear();

        int count = 0;
        for (double aoi = settings.AoiMin; aoi <= settings.AoiMax + 0.001; aoi += Math.Max(settings.AoiStep, 1))
        {
            for (double wl = settings.WavelengthMin; wl <= settings.WavelengthMax + 0.0001; wl += settings.WavelengthStep)
            {
                _design.AddTarget(settings.TargetType, Math.Round(wl, 4), Math.Round(aoi, 1),
                    settings.TargetValue, settings.CompareType, settings.Weight);
                Targets.Add(new MeritTargetViewModel(_design.MeritTargets[^1]));
                count++;
            }

            // If AOI min == AOI max, only one angle iteration
            if (Math.Abs(settings.AoiMax - settings.AoiMin) < 0.01) break;
        }

        UpdateChart();
        StatusText = $"Generated {count} targets";
    }

    // Optimization commands
    [RelayCommand]
    private async Task OptimizeLocal()
    {
        if (IsOptimizing) return;

        IsOptimizing = true;
        StatusText = "Optimizing...";

        try
        {
            int maxIter = OptMaxIterations;
            var result = await Task.Run(() => _optimizer.Optimize(_design, maxIterations: maxIter));

            OptBestMerit = result.FinalMerit;

            // Sync UI
            for (int i = 0; i < Layers.Count; i++)
                Layers[i].NotifyThicknessChanged();

            UpdateChart();
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Optimization error: {ex.Message}";
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    [RelayCommand]
    private async Task OptimizeGlobal()
    {
        if (IsOptimizing) return;

        IsOptimizing = true;
        StatusText = "Global optimization...";
        _optimizationCts = new CancellationTokenSource();

        try
        {
            int maxTrials = OptNumTrials;
            int maxIter = OptMaxIterations;
            var ct = _optimizationCts.Token;

            var progress = new Progress<(int trial, int total, double bestMerit)>(p =>
            {
                OptBestMerit = p.bestMerit;
                StatusText = $"Global: trial {p.trial}/{p.total}, best merit = {p.bestMerit:F6}";
            });

            var result = await Task.Run(() => _optimizer.GlobalOptimize(
                _design, maxTrials: maxTrials, maxIterationsPerTrial: maxIter,
                progress: progress, cancellationToken: ct));

            OptBestMerit = result.FinalMerit;

            for (int i = 0; i < Layers.Count; i++)
                Layers[i].NotifyThicknessChanged();

            UpdateChart();
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Global optimization error: {ex.Message}";
        }
        finally
        {
            _optimizationCts?.Dispose();
            _optimizationCts = null;
            IsOptimizing = false;
        }
    }

    public void StopOptimization()
    {
        _optimizationCts?.Cancel();
    }

    // Design commands
    [RelayCommand]
    private void NewSingleLayer()
    {
        _design = CoatingDesign.CreateDefaultSingleLayer();
        SyncFromDesign();
        UpdateChart();
        StatusText = "New single-layer AR design";
    }

    [RelayCommand]
    private void NewVCoat()
    {
        _design = CoatingDesign.CreateDefaultVCoat();
        SyncFromDesign();
        UpdateChart();
        StatusText = "New V-coat design";
    }

    [RelayCommand]
    private void NewDesign()
    {
        _design = new CoatingDesign
        {
            Name = "NewCoating",
            SubstrateMaterial = "N-BK7",
            UseOpticalThickness = true,
            ReferenceWavelength_um = 0.55
        };
        SyncFromDesign();
        UpdateChart();
        StatusText = "New empty design";
    }

    // Catalog operations
    public void RefreshMaterialList()
    {
        AvailableMaterials.Clear();
        foreach (var name in _catalogService.CoatingMaterialNames)
            AvailableMaterials.Add(name);
    }

    public void AddCoatingMaterial(CoatingMaterial material)
    {
        _catalogService.AddCoatingMaterial(material);
        RefreshMaterialList();
        CatalogDirty = true;
        StatusText = $"Added coating material: {material.Name} (unsaved — use Catalog > Save Catalog)";
    }

    public string[] GetCatalogCoatingNames()
    {
        return _catalogService.CoatingNames.OrderBy(n => n).ToArray();
    }

    public void OpenCatalog(string path)
    {
        try
        {
            _catalogService.LoadCoatingCatalog(path);
            AvailableMaterials.Clear();
            foreach (var name in _catalogService.CoatingMaterialNames)
                AvailableMaterials.Add(name);
            StatusText = $"Opened catalog: {Path.GetFileName(path)} ({_catalogService.CoatingCatalog.MaterialCount} materials, {_catalogService.CoatingNames.Count()} coatings)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening catalog: {ex.Message}";
        }
    }

    public void SaveCatalog()
    {
        try
        {
            var path = _catalogService.CoatingCatalogPath;
            if (!string.IsNullOrEmpty(path))
            {
                _catalogService.SaveCoatingCatalog(path);
                CatalogDirty = false;
                StatusText = $"Catalog saved to {Path.GetFileName(path)}";
            }
            else
            {
                StatusText = "No catalog file path set — use Save Catalog As";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving catalog: {ex.Message}";
        }
    }

    public void LoadFromCatalog(string coatingName)
    {
        try
        {
            var coatingDef = _catalogService.GetCoating(coatingName);
            if (coatingDef == null)
            {
                StatusText = $"Coating '{coatingName}' not found in catalog";
                return;
            }

            _design = ConvertToCoatingDesign(coatingDef);
            SyncFromDesign();
            UpdateChart();
            StatusText = $"Loaded from catalog: {coatingName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading coating: {ex.Message}";
        }
    }

    public void SaveToCatalog(string coatingName)
    {
        try
        {
            var coatingDef = ConvertToCoatingDefinition(_design, coatingName);
            _catalogService.AddCoating(coatingDef);
            _design.Name = coatingName;
            CoatingName = coatingName;
            CatalogDirty = true;
            StatusText = $"Design saved to catalog: {coatingName} (unsaved — use Catalog > Save Catalog)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving to catalog: {ex.Message}";
        }
    }

    public void SaveCatalogAs(string path)
    {
        try
        {
            _catalogService.SaveCoatingCatalog(path);
            CatalogDirty = false;
            StatusText = $"Catalog saved to {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving catalog: {ex.Message}";
        }
    }

    private CoatingDesign ConvertToCoatingDesign(CoatingDefinition coatingDef)
    {
        bool hasOptical = coatingDef.Layers.Any(l => l.IsOpticalThickness);
        bool hasPhysical = coatingDef.Layers.Any(l => !l.IsOpticalThickness);
        bool useOptical = hasOptical || !hasPhysical;

        var design = new CoatingDesign
        {
            Name = coatingDef.Name,
            SubstrateMaterial = SubstrateMaterial ?? "N-BK7",
            UseOpticalThickness = useOptical,
            ReferenceWavelength_um = coatingDef.ReferenceWavelength_um
        };

        foreach (var layer in coatingDef.Layers)
        {
            double thickness = layer.Thickness;

            if (useOptical && !layer.IsOpticalThickness)
            {
                var (n, _) = layer.Material.GetNK(coatingDef.ReferenceWavelength_um);
                thickness = n * layer.Thickness / coatingDef.ReferenceWavelength_um;
            }
            else if (!useOptical && layer.IsOpticalThickness)
            {
                var (n, _) = layer.Material.GetNK(coatingDef.ReferenceWavelength_um);
                thickness = layer.Thickness * coatingDef.ReferenceWavelength_um / n;
            }

            design.Layers.Add(new DesignLayer
            {
                MaterialName = layer.Material.Name,
                Thickness = thickness,
                IsVariable = true,
                MinThickness = 0.01,
                MaxThickness = useOptical ? 2.0 : 1.0
            });
        }

        for (double wl = 0.45; wl <= 0.651; wl += 0.01)
            design.AddTarget(MeritTargetType.Rave, Math.Round(wl, 3), 0, 0, CompareType.LessOrEqual);

        return design;
    }

    private CoatingDefinition ConvertToCoatingDefinition(CoatingDesign design, string name)
    {
        var coatingDef = new CoatingDefinition
        {
            Name = name,
            ReferenceWavelength_um = design.ReferenceWavelength_um
        };

        foreach (var layer in design.Layers)
        {
            var material = _catalogService.GetCoatingMaterial(layer.MaterialName);
            material ??= CoatingMaterial.Custom(layer.MaterialName, 1.5);

            coatingDef.Layers.Add(new CoatingLayer
            {
                Material = material,
                Thickness = layer.Thickness,
                IsOpticalThickness = design.UseOpticalThickness
            });
        }

        return coatingDef;
    }

    // Text export
    public void ExportText(string filePath)
    {
        try
        {
            SyncToDesign();
            using var writer = new StreamWriter(filePath);

            writer.WriteLine($"# AR Coating Designer Export");
            writer.WriteLine($"# Coating: {_design.Name}");
            writer.WriteLine($"# Substrate: {_design.SubstrateMaterial}");
            writer.WriteLine($"# Reference Wavelength: {_design.ReferenceWavelength_um:F4} um");
            writer.WriteLine($"# Optical Thickness: {_design.UseOpticalThickness}");
            writer.WriteLine($"# Layers: {_design.Layers.Count}");
            for (int i = 0; i < _design.Layers.Count; i++)
            {
                var l = _design.Layers[i];
                writer.WriteLine($"#   Layer {i + 1}: {l.MaterialName}  T={l.Thickness:F4}");
            }
            writer.WriteLine($"# Merit: {MeritValue:F6}");
            writer.WriteLine();

            if (PlotVsWavelength)
            {
                writer.WriteLine($"# R vs Wavelength  AOI={PlotAOI:F1} deg");
                writer.WriteLine("Wavelength_um\tRs\tRp\tRave");
            }
            else
            {
                writer.WriteLine($"# R vs Angle  Wavelength={PlotWavelengthForAngle:F4} um");
                writer.WriteLine("AOI_deg\tRs\tRp\tRave");
            }

            for (int i = 0; i < ChartX.Length; i++)
                writer.WriteLine($"{ChartX[i]:F4}\t{ChartRs[i]:F4}\t{ChartRp[i]:F4}\t{ChartRave[i]:F4}");

            StatusText = $"Exported to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export error: {ex.Message}";
        }
    }

    // ZEMAX export — writes all catalog materials and coatings
    public void ExportToZemaxDat(string filePath)
    {
        try
        {
            SyncToDesign();
            var catalog = _catalogService.CoatingCatalog;

            using var writer = new StreamWriter(filePath);
            writer.WriteLine("! ZEMAX Coating File");
            writer.WriteLine($"! Generated from ARCoatingDesigner");
            writer.WriteLine($"! Materials: {catalog.MaterialCount}  Coatings: {catalog.Count}");
            writer.WriteLine();

            // Write all materials as MATE entries with N-K tables
            foreach (var material in catalog.Materials)
            {
                string matName = SanitizeMaterialName(material.Name);
                writer.WriteLine($"MATE {matName}");

                // Write N-K table from 0.35 to 0.75 um in 0.01 um steps (41 points)
                for (double wl = 0.35; wl <= 0.7505; wl += 0.01)
                {
                    double wlRound = Math.Round(wl, 4);
                    var (n, k) = material.GetNK(wlRound);
                    writer.WriteLine($"  {wlRound:F4} {n:F6} {Math.Abs(k):F6}");
                }
                writer.WriteLine();
            }

            // Write all coatings as COAT entries
            foreach (var coating in catalog.Coatings)
            {
                string coatName = SanitizeMaterialName(coating.Name);
                writer.WriteLine($"COAT {coatName}");

                foreach (var layer in coating.Layers)
                {
                    string matName = SanitizeMaterialName(layer.Material.Name);
                    if (layer.IsOpticalThickness)
                        writer.WriteLine($"{matName} {layer.Thickness:G10}");
                    else
                        writer.WriteLine($"{matName} {layer.Thickness:G10} 1");
                }
                writer.WriteLine();
            }

            StatusText = $"Exported {catalog.MaterialCount} materials, {catalog.Count} coatings to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export error: {ex.Message}";
        }
    }

    private static string SanitizeMaterialName(string name)
    {
        string sanitized = name.ToUpperInvariant().Replace(" ", "");
        if (sanitized.Length > 16) sanitized = sanitized[..16];
        return sanitized;
    }

    // Load glass catalog
    [RelayCommand]
    private void LoadGlassCatalog(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            _catalogService.LoadGlassCatalog(path);
            AvailableCatalogs.Clear();
            AvailableCatalogs.Add("(All)");
            foreach (var name in _catalogService.GlassCatalogNames)
                AvailableCatalogs.Add(name);
            SelectedCatalog = "(All)";
            RefreshGlassList();
            StatusText = $"Loaded glass catalog: {_catalogService.GlassNames.Count()} glasses";
            UpdateChart();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load glass catalog: {ex.Message}";
        }
    }

    // Load coating catalog
    [RelayCommand]
    private void LoadCoatingCatalog(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            _catalogService.LoadCoatingCatalog(path);
            AvailableMaterials.Clear();
            foreach (var name in _catalogService.CoatingMaterialNames)
                AvailableMaterials.Add(name);
            StatusText = $"Loaded coating catalog: {_catalogService.CoatingCatalog.MaterialCount} materials, {_catalogService.CoatingCatalog.Count} coatings";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load coating catalog: {ex.Message}";
        }
    }

    // Save coating catalog
    [RelayCommand]
    private void SaveCoatingCatalog(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            _catalogService.SaveCoatingCatalog(path);
            StatusText = $"Saved coating catalog to {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save coating catalog: {ex.Message}";
        }
    }
}
