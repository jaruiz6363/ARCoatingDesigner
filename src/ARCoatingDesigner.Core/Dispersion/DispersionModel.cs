namespace ARCoatingDesigner.Core.Dispersion
{
    /// <summary>
    /// Dispersion model type for coating materials.
    /// </summary>
    public enum DispersionModel
    {
        /// <summary>Fixed n, k values (no wavelength dependence)</summary>
        Constant,

        /// <summary>Interpolated n,k data from tabulated values</summary>
        Tabulated,

        /// <summary>Cauchy formula: n(λ) = A + B/λ² + C/λ⁴ (λ in microns)</summary>
        Cauchy,

        /// <summary>
        /// Standard Sellmeier formula: n² = 1 + Σ Bᵢλ²/(λ²-Cᵢ)
        /// where Cᵢ is the resonance wavelength squared (in μm²).
        /// </summary>
        Sellmeier,

        /// <summary>
        /// Modified Sellmeier: n² = A + Σ Bᵢλ²/(λ²-Cᵢ)
        /// Some materials (like TiO2) use A ≠ 1.
        /// </summary>
        SellmeierModified
    }
}
