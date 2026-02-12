using CommunityToolkit.Mvvm.ComponentModel;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.App.ViewModels;

public partial class GenerateMeritViewModel : ObservableObject
{
    [ObservableProperty] private MeritTargetType _targetType = MeritTargetType.Rave;
    [ObservableProperty] private CompareType _compareType = CompareType.Equal;
    [ObservableProperty] private double _targetValue = 0;
    [ObservableProperty] private double _wavelengthMin = 0.45;
    [ObservableProperty] private double _wavelengthMax = 0.66;
    [ObservableProperty] private double _wavelengthStep = 0.005;
    [ObservableProperty] private double _aoiMin = 0;
    [ObservableProperty] private double _aoiMax = 0;
    [ObservableProperty] private double _aoiStep = 15;
    [ObservableProperty] private double _weight = 1.0;

    public int TargetTypeIndex
    {
        get => (int)TargetType;
        set { TargetType = (MeritTargetType)value; OnPropertyChanged(); }
    }

    public int CompareTypeIndex
    {
        get => (int)CompareType;
        set { CompareType = (CompareType)value; OnPropertyChanged(); }
    }

    public bool DialogResult { get; set; }
}
