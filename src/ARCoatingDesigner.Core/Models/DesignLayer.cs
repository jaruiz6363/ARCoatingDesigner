namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Represents a single layer in a coating design with optimization constraints.
    /// </summary>
    public class DesignLayer
    {
        public string MaterialName { get; set; } = "MgF2";

        /// <summary>
        /// Layer thickness. Interpretation depends on parent design's UseOpticalThickness setting:
        /// Physical: thickness in microns. Optical: wavelength fraction (0.25 = λ/4).
        /// </summary>
        public double Thickness { get; set; } = 0.1;

        public bool IsVariable { get; set; } = true;
        public double MinThickness { get; set; } = 0.01;
        public double MaxThickness { get; set; } = 1.0;

        public static double OpticalToPhysical(double opticalThickness, double refractiveIndex, double referenceWavelength_um)
        {
            return opticalThickness * referenceWavelength_um / refractiveIndex;
        }

        public static double PhysicalToOptical(double physical_um, double refractiveIndex, double referenceWavelength_um)
        {
            return refractiveIndex * physical_um / referenceWavelength_um;
        }

        public DesignLayer Clone()
        {
            return new DesignLayer
            {
                MaterialName = this.MaterialName,
                Thickness = this.Thickness,
                IsVariable = this.IsVariable,
                MinThickness = this.MinThickness,
                MaxThickness = this.MaxThickness
            };
        }
    }
}
