using System;

namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Single layer in a thin film coating stack.
    /// </summary>
    public class CoatingLayer
    {
        public CoatingMaterial Material { get; set; } = new CoatingMaterial();

        /// <summary>
        /// Layer thickness. If IsOpticalThickness: waves (0.25 = QWOT). Otherwise: physical microns.
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// If true, Thickness is in waves. Physical thickness = Thickness * wavelength / n.
        /// </summary>
        public bool IsOpticalThickness { get; set; } = false;

        /// <summary>
        /// Get physical thickness in microns for a given wavelength.
        /// </summary>
        public double GetPhysicalThickness_um(double wavelength_um)
        {
            if (IsOpticalThickness)
            {
                var (n, k) = Material.GetNK(wavelength_um);
                if (n > 0)
                    return Thickness * wavelength_um / n;
            }
            return Thickness;
        }

        public static CoatingLayer QuarterWave(CoatingMaterial material)
        {
            return new CoatingLayer
            {
                Material = material,
                Thickness = 0.25,
                IsOpticalThickness = true
            };
        }

        public static CoatingLayer HalfWave(CoatingMaterial material)
        {
            return new CoatingLayer
            {
                Material = material,
                Thickness = 0.5,
                IsOpticalThickness = true
            };
        }

        public static CoatingLayer Physical(CoatingMaterial material, double thickness_um)
        {
            return new CoatingLayer
            {
                Material = material,
                Thickness = thickness_um,
                IsOpticalThickness = false
            };
        }

        public static CoatingLayer Optical(CoatingMaterial material, double waves)
        {
            return new CoatingLayer
            {
                Material = material,
                Thickness = waves,
                IsOpticalThickness = true
            };
        }
    }
}
