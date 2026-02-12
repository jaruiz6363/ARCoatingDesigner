using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.App.ViewModels;

public partial class DesignLayerViewModel : ObservableObject
{
    private readonly DesignLayer _layer;

    public event Action? LayerChanged;

    public DesignLayerViewModel(DesignLayer layer)
    {
        _layer = layer;
    }

    public DesignLayer Model => _layer;

    public string MaterialName
    {
        get => _layer.MaterialName;
        set { _layer.MaterialName = value; OnPropertyChanged(); LayerChanged?.Invoke(); }
    }

    public double Thickness
    {
        get => _layer.Thickness;
        set { _layer.Thickness = value; OnPropertyChanged(); LayerChanged?.Invoke(); }
    }

    public bool IsVariable
    {
        get => _layer.IsVariable;
        set { _layer.IsVariable = value; OnPropertyChanged(); }
    }

    public double MinThickness
    {
        get => _layer.MinThickness;
        set { _layer.MinThickness = value; OnPropertyChanged(); }
    }

    public double MaxThickness
    {
        get => _layer.MaxThickness;
        set { _layer.MaxThickness = value; OnPropertyChanged(); }
    }

    public void NotifyThicknessChanged()
    {
        OnPropertyChanged(nameof(Thickness));
        OnPropertyChanged(nameof(MinThickness));
        OnPropertyChanged(nameof(MaxThickness));
    }
}
