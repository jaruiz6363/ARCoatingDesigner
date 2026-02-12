using System;
using System.Collections.Generic;
using System.Linq;

namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Represents a complete coating design with layers, substrate, and optimization targets.
    /// </summary>
    public class CoatingDesign
    {
        public string Name { get; set; } = "NewCoating";
        public string SubstrateMaterial { get; set; } = "N-BK7";
        public bool UseOpticalThickness { get; set; } = false;
        public double ReferenceWavelength_um { get; set; } = 0.55;
        public List<DesignLayer> Layers { get; set; } = new List<DesignLayer>();
        public List<MeritTarget> MeritTargets { get; set; } = new List<MeritTarget>();

        public IEnumerable<DesignLayer> GetVariableLayers() => Layers.Where(l => l.IsVariable);
        public IEnumerable<MeritTarget> GetActiveTargets() => MeritTargets.Where(t => t.UseTarget);

        public double[] GetVariableThicknesses() => GetVariableLayers().Select(l => l.Thickness).ToArray();
        public double[] GetLowerBounds() => GetVariableLayers().Select(l => l.MinThickness).ToArray();
        public double[] GetUpperBounds() => GetVariableLayers().Select(l => l.MaxThickness).ToArray();

        public void SetVariableThicknesses(double[] thicknesses)
        {
            var variableLayers = GetVariableLayers().ToList();
            if (thicknesses.Length != variableLayers.Count)
                throw new ArgumentException($"Expected {variableLayers.Count} thicknesses, got {thicknesses.Length}");

            for (int i = 0; i < variableLayers.Count; i++)
                variableLayers[i].Thickness = thicknesses[i];
        }

        public void AddLayer(string materialName = "MgF2", double thickness = 0.1)
        {
            Layers.Add(new DesignLayer
            {
                MaterialName = materialName,
                Thickness = thickness,
                IsVariable = true,
                MinThickness = thickness * 0.1,
                MaxThickness = thickness * 10.0
            });
        }

        public void RemoveLayerAt(int index)
        {
            if (index >= 0 && index < Layers.Count)
                Layers.RemoveAt(index);
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < Layers.Count)
            {
                var layer = Layers[index];
                Layers.RemoveAt(index);
                Layers.Insert(index - 1, layer);
            }
        }

        public void MoveLayerDown(int index)
        {
            if (index >= 0 && index < Layers.Count - 1)
            {
                var layer = Layers[index];
                Layers.RemoveAt(index);
                Layers.Insert(index + 1, layer);
            }
        }

        public void AddTarget(MeritTargetType type = MeritTargetType.Rave, double wavelength = 0.55, double aoi = 0,
            double targetValue = 0, CompareType compareType = CompareType.Equal, double weight = 1.0)
        {
            MeritTargets.Add(new MeritTarget
            {
                TargetType = type,
                TargetValue = targetValue,
                Wavelength_um = wavelength,
                AOI_deg = aoi,
                CompareType = compareType,
                Weight = weight,
                UseTarget = true
            });
        }

        public void RemoveTargetAt(int index)
        {
            if (index >= 0 && index < MeritTargets.Count)
                MeritTargets.RemoveAt(index);
        }

        public CoatingDesign Clone()
        {
            return new CoatingDesign
            {
                Name = this.Name,
                SubstrateMaterial = this.SubstrateMaterial,
                UseOpticalThickness = this.UseOpticalThickness,
                ReferenceWavelength_um = this.ReferenceWavelength_um,
                Layers = this.Layers.Select(l => l.Clone()).ToList(),
                MeritTargets = this.MeritTargets.Select(t => t.Clone()).ToList()
            };
        }

        public static CoatingDesign CreateDefaultSingleLayer()
        {
            var design = new CoatingDesign
            {
                Name = "SLAR",
                SubstrateMaterial = "N-BK7",
                UseOpticalThickness = true,
                ReferenceWavelength_um = 0.55
            };

            design.AddLayer("MgF2", 0.25);

            for (double wl = 0.45; wl <= 0.651; wl += 0.005)
                design.AddTarget(MeritTargetType.Rave, Math.Round(wl, 3), 0, 0, CompareType.Equal);

            return design;
        }

        public static CoatingDesign CreateDefaultVCoat()
        {
            var design = new CoatingDesign
            {
                Name = "VCoat",
                SubstrateMaterial = "N-BK7",
                UseOpticalThickness = true,
                ReferenceWavelength_um = 0.55
            };

            // Layer order: layers[0] = air side, layers[last] = substrate side.
            // Standard V-coat: Air / L / H / Substrate.
            design.AddLayer("MgF2", 0.3239);  // outermost (air side)
            design.AddLayer("TiO2", 0.0502);  // innermost (substrate side)

            // Narrow band around the design wavelength gives the optimizer
            // a smooth gradient to find the V-dip at 0.55 um.
            for (double wl = 0.50; wl <= 0.601; wl += 0.005)
                design.AddTarget(MeritTargetType.Rave, Math.Round(wl, 3), 0, 0, CompareType.Equal);

            return design;
        }
    }
}
