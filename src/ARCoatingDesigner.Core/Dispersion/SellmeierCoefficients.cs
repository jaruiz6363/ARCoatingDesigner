namespace ARCoatingDesigner.Core.Dispersion
{
    /// <summary>
    /// Sellmeier dispersion coefficients (up to 3 terms).
    /// n² = A + B₁λ²/(λ²-C₁) + B₂λ²/(λ²-C₂) + B₃λ²/(λ²-C₃)
    /// Standard Sellmeier has A = 1.
    /// </summary>
    public struct SellmeierCoefficients
    {
        public double A;
        public double B1, C1;
        public double B2, C2;
        public double B3, C3;

        /// <summary>
        /// Calculate n² from Sellmeier formula at given wavelength.
        /// </summary>
        public double CalcNSquared(double wavelength_um)
        {
            double lambda2 = wavelength_um * wavelength_um;
            double n2 = A;

            if (B1 != 0 || C1 != 0)
                n2 += B1 * lambda2 / (lambda2 - C1);
            if (B2 != 0 || C2 != 0)
                n2 += B2 * lambda2 / (lambda2 - C2);
            if (B3 != 0 || C3 != 0)
                n2 += B3 * lambda2 / (lambda2 - C3);

            return n2;
        }

        /// <summary>
        /// Create standard Sellmeier coefficients (A = 1).
        /// </summary>
        public static SellmeierCoefficients Standard(
            double b1, double c1,
            double b2 = 0, double c2 = 0,
            double b3 = 0, double c3 = 0)
        {
            return new SellmeierCoefficients
            {
                A = 1.0,
                B1 = b1, C1 = c1,
                B2 = b2, C2 = c2,
                B3 = b3, C3 = c3
            };
        }

        /// <summary>
        /// Create modified Sellmeier coefficients (custom A).
        /// </summary>
        public static SellmeierCoefficients Modified(double a,
            double b1, double c1,
            double b2 = 0, double c2 = 0,
            double b3 = 0, double c3 = 0)
        {
            return new SellmeierCoefficients
            {
                A = a,
                B1 = b1, C1 = c1,
                B2 = b2, C2 = c2,
                B3 = b3, C3 = c3
            };
        }
    }
}
