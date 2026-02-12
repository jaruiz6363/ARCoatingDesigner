using CommunityToolkit.Mvvm.ComponentModel;
using ARCoatingDesigner.Core.Dispersion;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.App.ViewModels;

public partial class AddMaterialViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _dispersionModelIndex = 0; // 0=Constant, 1=Cauchy, 2=Sellmeier

    // Constant fields
    [ObservableProperty] private double _constantN = 1.5;
    [ObservableProperty] private double _constantK = 0;

    // Cauchy fields
    [ObservableProperty] private double _cauchyA = 1.5;
    [ObservableProperty] private double _cauchyB = 0.005;
    [ObservableProperty] private double _cauchyC = 0;

    // Sellmeier fields
    [ObservableProperty] private double _sellmeierB1 = 0.6961663;
    [ObservableProperty] private double _sellmeierC1 = 0.0046791;
    [ObservableProperty] private double _sellmeierB2 = 0.4079426;
    [ObservableProperty] private double _sellmeierC2 = 0.0135121;
    [ObservableProperty] private double _sellmeierB3 = 0.8974794;
    [ObservableProperty] private double _sellmeierC3 = 97.934;

    [ObservableProperty] private string _errorMessage = "";

    public bool DialogResult { get; set; }

    public bool IsConstant => DispersionModelIndex == 0;
    public bool IsCauchy => DispersionModelIndex == 1;
    public bool IsSellmeier => DispersionModelIndex == 2;

    partial void OnDispersionModelIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsConstant));
        OnPropertyChanged(nameof(IsCauchy));
        OnPropertyChanged(nameof(IsSellmeier));
    }

    /// <summary>
    /// Validates input and builds a CoatingMaterial. Returns null if validation fails.
    /// </summary>
    public CoatingMaterial? BuildMaterial()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Material name is required.";
            return null;
        }

        try
        {
            return DispersionModelIndex switch
            {
                0 => CoatingMaterial.Custom(Name.Trim(), ConstantN, ConstantK),
                1 => CoatingMaterial.CustomCauchy(Name.Trim(), CauchyA, CauchyB, CauchyC),
                2 => CoatingMaterial.CustomSellmeier(
                         Name.Trim(),
                         SellmeierCoefficients.Standard(SellmeierB1, SellmeierC1, SellmeierB2, SellmeierC2, SellmeierB3, SellmeierC3),
                         fallbackN: 1.5),
                _ => null
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            return null;
        }
    }
}
