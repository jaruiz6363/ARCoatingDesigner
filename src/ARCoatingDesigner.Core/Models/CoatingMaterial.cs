using System;
using System.Collections.Generic;
using System.Linq;
using ARCoatingDesigner.Core.Dispersion;

namespace ARCoatingDesigner.Core.Models
{
    /// <summary>
    /// Material with wavelength-dependent n,k values for thin film calculations.
    /// Can use constant values, tabulated data, or dispersion formulas (Cauchy, Sellmeier).
    ///
    /// Sign convention (matches ZEMAX):
    ///   Complex refractive index: ñ = n + ik
    ///   - Non-absorbing (dielectric): k = 0
    ///   - Absorbing (metal): k &lt; 0 (negative)
    /// </summary>
    public class CoatingMaterial
    {
        public string Name { get; set; } = "";
        public DispersionModel Model { get; set; } = DispersionModel.Constant;
        public double RefractiveIndex { get; set; } = 1.5;
        public double ExtinctionCoefficient { get; set; } = 0;
        public Dictionary<double, (double n, double k)>? NKData { get; set; }
        public SellmeierCoefficients Sellmeier { get; set; }
        public CauchyCoefficients Cauchy { get; set; }

        /// <summary>
        /// Get refractive index and extinction coefficient at a given wavelength.
        /// </summary>
        public (double n, double k) GetNK(double wavelength_um)
        {
            double n;

            switch (Model)
            {
                case DispersionModel.Constant:
                    return (RefractiveIndex, ExtinctionCoefficient);

                case DispersionModel.Tabulated:
                    if (NKData != null && NKData.Count > 0)
                        return InterpolateNK(wavelength_um);
                    return (RefractiveIndex, ExtinctionCoefficient);

                case DispersionModel.Cauchy:
                    n = Cauchy.CalcN(wavelength_um);
                    return (n, ExtinctionCoefficient);

                case DispersionModel.Sellmeier:
                case DispersionModel.SellmeierModified:
                    double n2 = Sellmeier.CalcNSquared(wavelength_um);
                    n = n2 > 0 ? Math.Sqrt(n2) : RefractiveIndex;
                    return (n, ExtinctionCoefficient);

                default:
                    return (RefractiveIndex, ExtinctionCoefficient);
            }
        }

        private (double n, double k) InterpolateNK(double wavelength_um)
        {
            var sorted = NKData!.OrderBy(kvp => kvp.Key).ToList();

            if (sorted.Count == 1)
                return sorted[0].Value;

            if (wavelength_um <= sorted[0].Key)
                return sorted[0].Value;

            if (wavelength_um >= sorted[sorted.Count - 1].Key)
                return sorted[sorted.Count - 1].Value;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (wavelength_um >= sorted[i].Key && wavelength_um <= sorted[i + 1].Key)
                {
                    double t = (wavelength_um - sorted[i].Key) / (sorted[i + 1].Key - sorted[i].Key);
                    double n = sorted[i].Value.n + t * (sorted[i + 1].Value.n - sorted[i].Value.n);
                    double k = sorted[i].Value.k + t * (sorted[i + 1].Value.k - sorted[i].Value.k);
                    return (n, k);
                }
            }

            return (RefractiveIndex, ExtinctionCoefficient);
        }

        // Built-in materials

        public static CoatingMaterial MgF2 => new CoatingMaterial
        {
            Name = "MgF2",
            Model = DispersionModel.Sellmeier,
            RefractiveIndex = 1.38,
            ExtinctionCoefficient = 0,
            Sellmeier = SellmeierCoefficients.Standard(
                0.48755108, 0.001882178,
                0.39875031, 0.008951888,
                2.3120353, 566.13559)
        };

        public static CoatingMaterial SiO2 => new CoatingMaterial
        {
            Name = "SiO2",
            Model = DispersionModel.Sellmeier,
            RefractiveIndex = 1.46,
            ExtinctionCoefficient = 0,
            Sellmeier = SellmeierCoefficients.Standard(
                0.6961663, 0.0046791,
                0.4079426, 0.0135121,
                0.8974794, 97.9340)
        };

        public static CoatingMaterial Al2O3 => new CoatingMaterial
        {
            Name = "Al2O3",
            Model = DispersionModel.Sellmeier,
            RefractiveIndex = 1.77,
            ExtinctionCoefficient = 0,
            Sellmeier = SellmeierCoefficients.Standard(
                1.4313493, 0.0052799,
                0.65054713, 0.0142383,
                5.3414021, 325.01783)
        };

        public static CoatingMaterial ZrO2 => new CoatingMaterial
        {
            Name = "ZrO2",
            Model = DispersionModel.Cauchy,
            RefractiveIndex = 2.05,
            ExtinctionCoefficient = 0,
            Cauchy = new CauchyCoefficients { A = 1.92, B = 0.022, C = 0.002 }
        };

        public static CoatingMaterial Ta2O5 => new CoatingMaterial
        {
            Name = "Ta2O5",
            Model = DispersionModel.Cauchy,
            RefractiveIndex = 2.10,
            ExtinctionCoefficient = 0,
            Cauchy = new CauchyCoefficients { A = 1.97, B = 0.022, C = 0.002 }
        };

        public static CoatingMaterial TiO2 => new CoatingMaterial
        {
            Name = "TiO2",
            Model = DispersionModel.Cauchy,
            RefractiveIndex = 2.35,
            ExtinctionCoefficient = 0,
            Cauchy = new CauchyCoefficients { A = 2.20, B = 0.030, C = 0.003 }
        };

        public static CoatingMaterial HfO2 => new CoatingMaterial
        {
            Name = "HfO2",
            Model = DispersionModel.Cauchy,
            RefractiveIndex = 1.95,
            ExtinctionCoefficient = 0,
            Cauchy = new CauchyCoefficients { A = 1.84, B = 0.018, C = 0.002 }
        };

        public static CoatingMaterial Custom(string name, double n, double k = 0)
        {
            return new CoatingMaterial
            {
                Name = name,
                Model = DispersionModel.Constant,
                RefractiveIndex = n,
                ExtinctionCoefficient = k
            };
        }

        public static CoatingMaterial CustomSellmeier(string name, SellmeierCoefficients coefficients, double fallbackN = 1.5)
        {
            return new CoatingMaterial
            {
                Name = name,
                Model = DispersionModel.Sellmeier,
                RefractiveIndex = fallbackN,
                ExtinctionCoefficient = 0,
                Sellmeier = coefficients
            };
        }

        public static CoatingMaterial CustomCauchy(string name, double A, double B, double C = 0)
        {
            return new CoatingMaterial
            {
                Name = name,
                Model = DispersionModel.Cauchy,
                RefractiveIndex = A,
                ExtinctionCoefficient = 0,
                Cauchy = new CauchyCoefficients { A = A, B = B, C = C }
            };
        }
    }
}
