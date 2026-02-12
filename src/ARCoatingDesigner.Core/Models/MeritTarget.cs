using System;

namespace ARCoatingDesigner.Core.Models
{
    public enum MeritTargetType
    {
        Rs,
        Rp,
        Rave,
        Ts,
        Tp,
        Tave
    }

    public enum CompareType
    {
        Equal,
        LessOrEqual,
        GreaterOrEqual
    }

    /// <summary>
    /// Represents a single target in the merit function for coating optimization.
    /// </summary>
    public class MeritTarget
    {
        public bool UseTarget { get; set; } = true;
        public MeritTargetType TargetType { get; set; } = MeritTargetType.Rave;
        public double TargetValue { get; set; } = 0.5;
        public double Weight { get; set; } = 1.0;
        public CompareType CompareType { get; set; } = CompareType.LessOrEqual;
        public double Wavelength_um { get; set; } = 0.55;
        public double AOI_deg { get; set; } = 0.0;

        /// <summary>
        /// Compute the merit contribution for this target given a calculated value.
        /// </summary>
        public double ComputeMerit(double calculatedValue)
        {
            if (!UseTarget)
                return 0.0;

            double error = 0.0;

            switch (CompareType)
            {
                case CompareType.Equal:
                    error = calculatedValue - TargetValue;
                    break;
                case CompareType.LessOrEqual:
                    error = Math.Max(0, calculatedValue - TargetValue);
                    break;
                case CompareType.GreaterOrEqual:
                    error = Math.Max(0, TargetValue - calculatedValue);
                    break;
            }

            return Weight * error * error;
        }

        public MeritTarget Clone()
        {
            return new MeritTarget
            {
                UseTarget = this.UseTarget,
                TargetType = this.TargetType,
                TargetValue = this.TargetValue,
                Weight = this.Weight,
                CompareType = this.CompareType,
                Wavelength_um = this.Wavelength_um,
                AOI_deg = this.AOI_deg
            };
        }
    }
}
