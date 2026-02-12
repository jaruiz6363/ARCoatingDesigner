namespace ARCoatingDesigner.Core.Dispersion
{
    /// <summary>
    /// Cauchy dispersion coefficients.
    /// n(λ) = A + B/λ² + C/λ⁴ (λ in microns)
    /// </summary>
    public struct CauchyCoefficients
    {
        public double A;
        public double B;
        public double C;

        /// <summary>
        /// Calculate n from Cauchy formula at given wavelength.
        /// </summary>
        public double CalcN(double wavelength_um)
        {
            double lambda2 = wavelength_um * wavelength_um;
            double lambda4 = lambda2 * lambda2;
            return A + B / lambda2 + C / lambda4;
        }
    }
}
