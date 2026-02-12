namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Results of thin film R/T calculation.
    /// </summary>
    public struct ThinFilmResult
    {
        /// <summary>S-polarized reflectance (0-100%)</summary>
        public double Rs;
        /// <summary>P-polarized reflectance (0-100%)</summary>
        public double Rp;
        /// <summary>Average reflectance (0-100%)</summary>
        public double Rave;
        /// <summary>S-polarized transmittance (0-100%)</summary>
        public double Ts;
        /// <summary>P-polarized transmittance (0-100%)</summary>
        public double Tp;
        /// <summary>Average transmittance (0-100%)</summary>
        public double Tave;
        /// <summary>True if TIR occurred</summary>
        public bool IsTIR;
    }
}
