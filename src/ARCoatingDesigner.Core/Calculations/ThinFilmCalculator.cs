using System;
using System.Numerics;
using ARCoatingDesigner.Core.Models;
using ARCoatingDesigner.Core.Catalogs;

namespace ARCoatingDesigner.Core.Calculations
{
    /// <summary>
    /// 2x2 complex matrix for transfer matrix method.
    /// </summary>
    public struct ComplexMatrix2x2
    {
        public Complex M11, M12, M21, M22;

        public ComplexMatrix2x2(Complex m11, Complex m12, Complex m21, Complex m22)
        {
            M11 = m11; M12 = m12; M21 = m21; M22 = m22;
        }

        public static ComplexMatrix2x2 Identity => new ComplexMatrix2x2(
            Complex.One, Complex.Zero, Complex.Zero, Complex.One);

        public static ComplexMatrix2x2 Multiply(ComplexMatrix2x2 a, ComplexMatrix2x2 b)
        {
            return new ComplexMatrix2x2(
                a.M11 * b.M11 + a.M12 * b.M21,
                a.M11 * b.M12 + a.M12 * b.M22,
                a.M21 * b.M11 + a.M22 * b.M21,
                a.M21 * b.M12 + a.M22 * b.M22);
        }
    }

    /// <summary>
    /// Service for calculating thin film reflectance and transmittance
    /// using the transfer matrix method.
    /// </summary>
    public class ThinFilmCalculator
    {
        private readonly CatalogService _catalogService;

        public ThinFilmCalculator(CatalogService catalogService)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        }

        /// <summary>
        /// Calculate reflectance and transmittance for a coating design.
        /// </summary>
        public ThinFilmResult Calculate(CoatingDesign design, double wavelength_um, double aoi_deg)
        {
            var coating = ConvertToCoatingDefinition(design, wavelength_um);
            double n_substrate = _catalogService.GetGlassIndex(design.SubstrateMaterial, wavelength_um);
            double n_incident = 1.0; // Air
            double aoi_rad = aoi_deg * Math.PI / 180.0;

            bool notTIR = CalculateCoatingCoefficients(
                coating, wavelength_um,
                n_incident, n_substrate, aoi_rad,
                out Complex ts, out Complex tp, out Complex rs, out Complex rp);

            var result = new ThinFilmResult();

            if (!notTIR)
            {
                result.IsTIR = true;
                result.Rs = 100.0;
                result.Rp = 100.0;
                result.Rave = 100.0;
                result.Ts = 0.0;
                result.Tp = 0.0;
                result.Tave = 0.0;
                return result;
            }

            // Convert amplitude coefficients to power reflectance
            result.Rs = (rs * Complex.Conjugate(rs)).Real * 100.0;
            result.Rp = (rp * Complex.Conjugate(rp)).Real * 100.0;
            result.Rave = (result.Rs + result.Rp) / 2.0;

            // Transmittance includes geometric factors (different for s and p)
            double cos_aoi = Math.Cos(aoi_rad);
            double sin_aoi = Math.Sin(aoi_rad);
            double sin_aot = n_incident * sin_aoi / n_substrate;
            double cos_aot = Math.Sqrt(Math.Max(0, 1.0 - sin_aot * sin_aot));

            // s-polarization: η_sub/η_0 = (n_sub·cos_θt)/(n_0·cos_θi)
            double geoFactor_s = (n_substrate * cos_aot) / (n_incident * cos_aoi);
            // p-polarization: η_sub/η_0 = (n_sub/cos_θt)/(n_0/cos_θi) = (n_sub·cos_θi)/(n_0·cos_θt)
            double geoFactor_p = (n_substrate * cos_aoi) / (n_incident * cos_aot);

            result.Ts = (ts * Complex.Conjugate(ts)).Real * geoFactor_s * 100.0;
            result.Tp = (tp * Complex.Conjugate(tp)).Real * geoFactor_p * 100.0;
            result.Tave = (result.Ts + result.Tp) / 2.0;

            result.IsTIR = false;
            return result;
        }

        /// <summary>
        /// Calculate spectral response (R vs wavelength).
        /// </summary>
        public (double[] wavelengths, double[] Rs, double[] Rp, double[] Rave) CalculateSpectrum(
            CoatingDesign design, double wavelengthMin_um, double wavelengthMax_um, int numPoints, double aoi_deg)
        {
            var wavelengths = new double[numPoints];
            var Rs = new double[numPoints];
            var Rp = new double[numPoints];
            var Rave = new double[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                double t = (double)i / (numPoints - 1);
                wavelengths[i] = wavelengthMin_um + t * (wavelengthMax_um - wavelengthMin_um);

                var result = Calculate(design, wavelengths[i], aoi_deg);
                Rs[i] = result.Rs;
                Rp[i] = result.Rp;
                Rave[i] = result.Rave;
            }

            return (wavelengths, Rs, Rp, Rave);
        }

        /// <summary>
        /// Calculate angular response (R vs angle).
        /// </summary>
        public (double[] angles, double[] Rs, double[] Rp, double[] Rave) CalculateAngularResponse(
            CoatingDesign design, double wavelength_um, double angleMin_deg, double angleMax_deg, int numPoints)
        {
            var angles = new double[numPoints];
            var Rs = new double[numPoints];
            var Rp = new double[numPoints];
            var Rave = new double[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                double t = (double)i / (numPoints - 1);
                angles[i] = angleMin_deg + t * (angleMax_deg - angleMin_deg);

                var result = Calculate(design, wavelength_um, angles[i]);
                Rs[i] = result.Rs;
                Rp[i] = result.Rp;
                Rave[i] = result.Rave;
            }

            return (angles, Rs, Rp, Rave);
        }

        /// <summary>
        /// Get the value for a specific merit target.
        /// </summary>
        public double GetTargetValue(CoatingDesign design, MeritTarget target)
        {
            var result = Calculate(design, target.Wavelength_um, target.AOI_deg);

            return target.TargetType switch
            {
                MeritTargetType.Rs => result.Rs,
                MeritTargetType.Rp => result.Rp,
                MeritTargetType.Rave => result.Rave,
                MeritTargetType.Ts => result.Ts,
                MeritTargetType.Tp => result.Tp,
                MeritTargetType.Tave => result.Tave,
                _ => result.Rave,
            };
        }

        /// <summary>
        /// Calculate total merit function value.
        /// </summary>
        public double CalculateMerit(CoatingDesign design)
        {
            double totalMerit = 0.0;

            foreach (var target in design.GetActiveTargets())
            {
                double calculatedValue = GetTargetValue(design, target);
                totalMerit += target.ComputeMerit(calculatedValue);
            }

            return totalMerit;
        }

        /// <summary>
        /// Convert CoatingDesign to CoatingDefinition for calculation.
        /// </summary>
        private CoatingDefinition ConvertToCoatingDefinition(CoatingDesign design, double wavelength_um)
        {
            var coating = new CoatingDefinition
            {
                Name = design.Name,
                ReferenceWavelength_um = design.ReferenceWavelength_um
            };

            foreach (var layer in design.Layers)
            {
                var material = _catalogService.GetCoatingMaterial(layer.MaterialName);
                if (material == null)
                    material = CoatingMaterial.Custom(layer.MaterialName, 1.5);

                double thickness = layer.Thickness;

                if (design.UseOpticalThickness)
                {
                    var (n, k) = material.GetNK(design.ReferenceWavelength_um);
                    thickness = layer.Thickness * design.ReferenceWavelength_um / n;
                }

                var coatingLayer = new CoatingLayer
                {
                    Material = material,
                    Thickness = thickness,
                    IsOpticalThickness = false
                };

                coating.Layers.Add(coatingLayer);
            }

            return coating;
        }

        // =====================================================================
        // Transfer Matrix Method (TMM) Engine
        // =====================================================================

        /// <summary>
        /// Calculate Fresnel amplitude coefficients for bare surface.
        /// </summary>
        public static bool CalculateFresnelCoefficients(
            double n1, double n2, double cos_theta_i,
            out Complex ts, out Complex tp, out Complex rs, out Complex rp)
        {
            ts = tp = rs = rp = Complex.Zero;

            cos_theta_i = Math.Abs(cos_theta_i);
            if (cos_theta_i > 1.0) cos_theta_i = 1.0;

            double sin_theta_i = Math.Sqrt(1.0 - cos_theta_i * cos_theta_i);
            double sin_theta_t = (n1 / n2) * sin_theta_i;

            if (sin_theta_t > 1.0)
            {
                // Total internal reflection
                double cos_t_sq = 1.0 - sin_theta_t * sin_theta_t;
                Complex cos_t_complex = new Complex(0, Math.Sqrt(-cos_t_sq));

                Complex n1ci = new Complex(n1 * cos_theta_i, 0);
                Complex n2ct = n2 * cos_t_complex;
                Complex n1ct = n1 * cos_t_complex;
                Complex n2ci = new Complex(n2 * cos_theta_i, 0);

                rs = (n1ci - n2ct) / (n1ci + n2ct);
                rp = (n2ci - n1ct) / (n2ci + n1ct);
                ts = Complex.Zero;
                tp = Complex.Zero;

                return false;
            }

            double cos_theta_t = Math.Sqrt(1.0 - sin_theta_t * sin_theta_t);

            double n1_cos_i = n1 * cos_theta_i;
            double n2_cos_t = n2 * cos_theta_t;
            double n1_cos_t = n1 * cos_theta_t;
            double n2_cos_i = n2 * cos_theta_i;

            rs = new Complex((n1_cos_i - n2_cos_t) / (n1_cos_i + n2_cos_t), 0);
            ts = new Complex(2.0 * n1_cos_i / (n1_cos_i + n2_cos_t), 0);

            rp = new Complex((n1_cos_t - n2_cos_i) / (n1_cos_t + n2_cos_i), 0);
            tp = new Complex(2.0 * n1_cos_i / (n1_cos_t + n2_cos_i), 0);

            return true;
        }

        /// <summary>
        /// Calculate the characteristic matrix for a single thin film layer.
        /// </summary>
        private static ComplexMatrix2x2 CalculateLayerMatrix(
            Complex n_layer,
            double thickness_um,
            double wavelength_um,
            Complex cos_theta_layer,
            int polarization)
        {
            // Phase thickness: delta = 2*pi*n*d*cos(theta)/lambda
            Complex delta = 2.0 * Math.PI * n_layer * thickness_um * cos_theta_layer / wavelength_um;

            Complex cos_delta = Complex.Cos(delta);
            Complex sin_delta = Complex.Sin(delta);

            // Optical admittance
            // s-pol: eta = n * cos(theta)
            // p-pol: eta = n / cos(theta)
            Complex eta;
            if (polarization == 0)
                eta = n_layer * cos_theta_layer;
            else
                eta = n_layer / cos_theta_layer;

            // Characteristic matrix:
            // [cos(delta)           i*sin(delta)/eta]
            // [i*eta*sin(delta)     cos(delta)      ]
            return new ComplexMatrix2x2(
                cos_delta,
                Complex.ImaginaryOne * sin_delta / eta,
                Complex.ImaginaryOne * eta * sin_delta,
                cos_delta);
        }

        /// <summary>
        /// Calculate transmission and reflection coefficients for a multi-layer coating
        /// using the transfer matrix method (TMM).
        /// </summary>
        public static bool CalculateCoatingCoefficients(
            CoatingDefinition coating,
            double wavelength_um,
            double n_incident,
            double n_substrate,
            double aoi_rad,
            out Complex ts, out Complex tp, out Complex rs, out Complex rp,
            double designWavelength_um = 0)
        {
            ts = tp = rs = rp = Complex.Zero;

            if (coating == null || coating.Layers.Count == 0)
            {
                double cos_aoi = Math.Cos(aoi_rad);
                return CalculateFresnelCoefficients(n_incident, n_substrate, cos_aoi,
                    out ts, out tp, out rs, out rp);
            }

            double sin_theta_0 = Math.Sin(aoi_rad);
            double cos_theta_0 = Math.Cos(aoi_rad);

            // Check for TIR at substrate
            double sin_theta_sub = n_incident * sin_theta_0 / n_substrate;
            if (sin_theta_sub > 1.0)
                return false;

            double cos_theta_sub = Math.Sqrt(1.0 - sin_theta_sub * sin_theta_sub);

            // Build transfer matrices for s and p polarizations
            ComplexMatrix2x2 M_s = ComplexMatrix2x2.Identity;
            ComplexMatrix2x2 M_p = ComplexMatrix2x2.Identity;

            // Reverse layer order for glass-to-air interfaces
            bool reverseLayerOrder = n_incident > n_substrate;
            int layerCount = coating.Layers.Count;

            double thicknessConversionWavelength = (designWavelength_um > 0) ? designWavelength_um : coating.ReferenceWavelength_um;

            for (int i = 0; i < layerCount; i++)
            {
                int layerIndex = reverseLayerOrder ? (layerCount - 1 - i) : i;
                var layer = coating.Layers[layerIndex];

                var (n, k) = layer.Material.GetNK(wavelength_um);
                Complex n_layer = new Complex(n, k);

                // Snell's law with complex n
                Complex sin_theta_layer = n_incident * sin_theta_0 / n_layer;
                Complex cos_theta_layer = Complex.Sqrt(1.0 - sin_theta_layer * sin_theta_layer);

                // Correct branch
                if (cos_theta_layer.Real < 0 || (cos_theta_layer.Real == 0 && cos_theta_layer.Imaginary < 0))
                    cos_theta_layer = -cos_theta_layer;

                double thickness_um = layer.GetPhysicalThickness_um(thicknessConversionWavelength);

                var M_s_layer = CalculateLayerMatrix(n_layer, thickness_um, wavelength_um, cos_theta_layer, 0);
                var M_p_layer = CalculateLayerMatrix(n_layer, thickness_um, wavelength_um, cos_theta_layer, 1);

                M_s = ComplexMatrix2x2.Multiply(M_s, M_s_layer);
                M_p = ComplexMatrix2x2.Multiply(M_p, M_p_layer);
            }

            // Optical admittances for incident and exit media
            double eta_0_s = n_incident * cos_theta_0;
            double eta_sub_s = n_substrate * cos_theta_sub;
            double eta_0_p = n_incident / cos_theta_0;
            double eta_sub_p = n_substrate / cos_theta_sub;

            // r = (eta_0*B - C) / (eta_0*B + C)
            // t = 2*eta_0 / (eta_0*B + C)

            // s-polarization
            Complex B_s = M_s.M11 + M_s.M12 * eta_sub_s;
            Complex C_s = M_s.M21 + M_s.M22 * eta_sub_s;
            Complex denom_s = eta_0_s * B_s + C_s;
            rs = (eta_0_s * B_s - C_s) / denom_s;
            ts = 2.0 * eta_0_s / denom_s;

            // p-polarization
            Complex B_p = M_p.M11 + M_p.M12 * eta_sub_p;
            Complex C_p = M_p.M21 + M_p.M22 * eta_sub_p;
            Complex denom_p = eta_0_p * B_p + C_p;
            rp = (eta_0_p * B_p - C_p) / denom_p;
            tp = 2.0 * eta_0_p / denom_p;

            return true;
        }
    }
}
