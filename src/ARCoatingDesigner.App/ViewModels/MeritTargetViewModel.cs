using CommunityToolkit.Mvvm.ComponentModel;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.App.ViewModels;

public partial class MeritTargetViewModel : ObservableObject
{
    private readonly MeritTarget _target;

    [ObservableProperty]
    private double _calculatedValue;

    public MeritTargetViewModel(MeritTarget target)
    {
        _target = target;
    }

    public MeritTarget Model => _target;

    public bool UseTarget
    {
        get => _target.UseTarget;
        set { _target.UseTarget = value; OnPropertyChanged(); }
    }

    public MeritTargetType TargetType
    {
        get => _target.TargetType;
        set { _target.TargetType = value; OnPropertyChanged(); }
    }

    public double TargetValue
    {
        get => _target.TargetValue;
        set { _target.TargetValue = value; OnPropertyChanged(); }
    }

    public double Weight
    {
        get => _target.Weight;
        set { _target.Weight = value; OnPropertyChanged(); }
    }

    public CompareType CompareType
    {
        get => _target.CompareType;
        set { _target.CompareType = value; OnPropertyChanged(); }
    }

    public double Wavelength_um
    {
        get => _target.Wavelength_um;
        set { _target.Wavelength_um = value; OnPropertyChanged(); }
    }

    public double AOI_deg
    {
        get => _target.AOI_deg;
        set { _target.AOI_deg = value; OnPropertyChanged(); }
    }
}
