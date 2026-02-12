using System.Collections.Generic;
using System.Linq;

namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Complete coating definition with multiple layers.
    /// Layers are ordered from incident medium to substrate.
    /// </summary>
    public class CoatingDefinition
    {
        public string Name { get; set; } = "";
        public double ReferenceWavelength_um { get; set; } = 0.55;
        public List<CoatingLayer> Layers { get; set; } = new List<CoatingLayer>();

        public double GetLayerPhysicalThickness_um(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= Layers.Count)
                return 0;
            return Layers[layerIndex].GetPhysicalThickness_um(ReferenceWavelength_um);
        }

        public static CoatingDefinition SingleLayerAR(double referenceWavelength_um = 0.55)
        {
            return new CoatingDefinition
            {
                Name = "SLAR",
                ReferenceWavelength_um = referenceWavelength_um,
                Layers = new List<CoatingLayer>
                {
                    CoatingLayer.QuarterWave(CoatingMaterial.MgF2)
                }
            };
        }

        public static CoatingDefinition VCoatAR(double referenceWavelength_um = 0.55)
        {
            return new CoatingDefinition
            {
                Name = "VCOAT",
                ReferenceWavelength_um = referenceWavelength_um,
                Layers = new List<CoatingLayer>
                {
                    CoatingLayer.QuarterWave(CoatingMaterial.TiO2),
                    CoatingLayer.QuarterWave(CoatingMaterial.MgF2)
                }
            };
        }

        public static CoatingDefinition BroadbandAR(double referenceWavelength_um = 0.55)
        {
            return new CoatingDefinition
            {
                Name = "BBAR",
                ReferenceWavelength_um = referenceWavelength_um,
                Layers = new List<CoatingLayer>
                {
                    CoatingLayer.QuarterWave(CoatingMaterial.MgF2),
                    CoatingLayer.HalfWave(CoatingMaterial.Al2O3),
                    CoatingLayer.QuarterWave(CoatingMaterial.MgF2)
                }
            };
        }

        public static CoatingDefinition Custom(string name, double referenceWavelength_um,
            params (CoatingMaterial material, double thickness_um)[] layers)
        {
            var coating = new CoatingDefinition
            {
                Name = name,
                ReferenceWavelength_um = referenceWavelength_um
            };

            foreach (var (material, thickness) in layers)
                coating.Layers.Add(CoatingLayer.Physical(material, thickness));

            return coating;
        }

        public override string ToString()
        {
            if (Layers.Count == 0)
                return $"{Name} @{ReferenceWavelength_um * 1000:F0}nm: (no layers)";

            var layerStrs = Layers.Select(l =>
            {
                double physicalThickness_nm = l.GetPhysicalThickness_um(ReferenceWavelength_um) * 1000;
                string thicknessStr = l.IsOpticalThickness
                    ? $"{l.Thickness:F2}W={physicalThickness_nm:F1}nm"
                    : $"{physicalThickness_nm:F1}nm";
                return $"{l.Material.Name}({thicknessStr})";
            });
            return $"{Name} @{ReferenceWavelength_um * 1000:F0}nm: {string.Join(" / ", layerStrs)}";
        }
    }
}
