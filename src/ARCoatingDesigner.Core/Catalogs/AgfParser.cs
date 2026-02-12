using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ARCoatingDesigner.Core.Catalogs
{
    /// <summary>
    /// Simple glass data for refractive index calculation.
    /// Supports Sellmeier (type 2) dispersion formula from AGF files.
    /// </summary>
    public class GlassData
    {
        public string Name { get; set; } = "";
        public int FormulaType { get; set; }
        public double[] Coefficients { get; set; } = Array.Empty<double>();
        public double LambdaMin { get; set; }
        public double LambdaMax { get; set; }

        /// <summary>
        /// Calculate refractive index at given wavelength using the dispersion formula.
        /// </summary>
        public double CalculateN(double wavelength_um)
        {
            double n2;
            double lam2 = wavelength_um * wavelength_um;

            switch (FormulaType)
            {
                case 1: // Schott
                    if (Coefficients.Length < 6) return 1.5;
                    n2 = Coefficients[0]
                         + Coefficients[1] * lam2
                         + Coefficients[2] / lam2
                         + Coefficients[3] / (lam2 * lam2)
                         + Coefficients[4] / (lam2 * lam2 * lam2)
                         + Coefficients[5] / (lam2 * lam2 * lam2 * lam2);
                    return n2 > 0 ? Math.Sqrt(n2) : 1.5;

                case 2: // Sellmeier 1
                    if (Coefficients.Length < 6) return 1.5;
                    n2 = 1.0
                         + Coefficients[0] * lam2 / (lam2 - Coefficients[1])
                         + Coefficients[2] * lam2 / (lam2 - Coefficients[3])
                         + Coefficients[4] * lam2 / (lam2 - Coefficients[5]);
                    return n2 > 0 ? Math.Sqrt(n2) : 1.5;

                case 3: // Herzberger
                    if (Coefficients.Length < 6) return 1.5;
                    double L = 1.0 / (lam2 - 0.028);
                    return Coefficients[0]
                           + Coefficients[1] * L
                           + Coefficients[2] * L * L
                           + Coefficients[3] * lam2
                           + Coefficients[4] * lam2 * lam2
                           + Coefficients[5] * lam2 * lam2 * lam2;

                case 4: // Sellmeier 2
                    if (Coefficients.Length < 5) return 1.5;
                    n2 = Coefficients[0]
                         + Coefficients[1] * lam2 / (lam2 - Coefficients[2])
                         + Coefficients[3] * lam2 / (lam2 - Coefficients[4]);
                    return n2 > 0 ? Math.Sqrt(n2) : 1.5;

                case 5: // Conrady
                    if (Coefficients.Length < 3) return 1.5;
                    return Coefficients[0]
                           + Coefficients[1] / wavelength_um
                           + Coefficients[2] / Math.Pow(wavelength_um, 3.5);

                case 6: // Sellmeier 3
                    if (Coefficients.Length < 8) return 1.5;
                    n2 = Coefficients[0]
                         + Coefficients[1] * lam2 / (lam2 - Coefficients[2])
                         + Coefficients[3] * lam2 / (lam2 - Coefficients[4])
                         + Coefficients[5] * lam2 / (lam2 - Coefficients[6])
                         + Coefficients[7] * lam2 / (lam2 - (Coefficients.Length > 8 ? Coefficients[8] : 0));
                    return n2 > 0 ? Math.Sqrt(n2) : 1.5;

                default:
                    // Use first coefficient as constant if unknown formula
                    return Coefficients.Length > 0 ? Coefficients[0] : 1.5;
            }
        }
    }

    /// <summary>
    /// Parser for ZEMAX AGF (glass catalog) files.
    /// </summary>
    public static class AgfParser
    {
        /// <summary>
        /// Load glass catalog from an AGF file.
        /// Returns dictionary of glass name -> GlassData.
        /// </summary>
        public static Dictionary<string, GlassData> LoadAgfFile(string filePath)
        {
            var glasses = new Dictionary<string, GlassData>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(filePath))
                return glasses;

            string[] lines = File.ReadAllLines(filePath);
            GlassData? current = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("NM ", StringComparison.OrdinalIgnoreCase))
                {
                    // NM <name> <formula_type> <...>
                    // Save previous glass
                    if (current != null && !string.IsNullOrEmpty(current.Name))
                        glasses[current.Name] = current;

                    current = new GlassData();
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        current.Name = parts[1];
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int formula))
                        current.FormulaType = formula;
                }
                else if (line.StartsWith("CD ", StringComparison.OrdinalIgnoreCase) && current != null)
                {
                    // CD <c0> <c1> <c2> ... (dispersion coefficients)
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var coeffs = new List<double>();
                    for (int j = 1; j < parts.Length; j++)
                    {
                        if (double.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                            coeffs.Add(val);
                    }
                    current.Coefficients = coeffs.ToArray();
                }
                else if (line.StartsWith("LD ", StringComparison.OrdinalIgnoreCase) && current != null)
                {
                    // LD <lambda_min> <lambda_max> (wavelength range in microns)
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lmin))
                            current.LambdaMin = lmin;
                        if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lmax))
                            current.LambdaMax = lmax;
                    }
                }
            }

            // Save last glass
            if (current != null && !string.IsNullOrEmpty(current.Name))
                glasses[current.Name] = current;

            return glasses;
        }
    }
}
