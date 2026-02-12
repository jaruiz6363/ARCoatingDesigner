using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ARCoatingDesigner.Core.Dispersion;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.Core.Catalogs
{
    /// <summary>
    /// Catalog of coating materials and definitions.
    /// Can be loaded from and saved to .coat text file format.
    /// </summary>
    public class CoatingCatalog
    {
        private Dictionary<string, CoatingMaterial> _materials =
            new Dictionary<string, CoatingMaterial>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, CoatingDefinition> _coatings =
            new Dictionary<string, CoatingDefinition>(StringComparer.OrdinalIgnoreCase);

        public string? FilePath { get; private set; }

        // Material management
        public void AddMaterial(CoatingMaterial material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (string.IsNullOrWhiteSpace(material.Name))
                throw new ArgumentException("Material must have a name", nameof(material));
            _materials[material.Name] = material;
        }

        public CoatingMaterial? GetMaterial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _materials.TryGetValue(name, out var material) ? material : null;
        }

        public bool ContainsMaterial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _materials.ContainsKey(name);
        }

        public int MaterialCount => _materials.Count;
        public IEnumerable<string> MaterialNames => _materials.Keys;
        public IEnumerable<CoatingMaterial> Materials => _materials.Values;

        public void AddStandardMaterials()
        {
            AddMaterial(CoatingMaterial.MgF2);
            AddMaterial(CoatingMaterial.SiO2);
            AddMaterial(CoatingMaterial.Al2O3);
            AddMaterial(CoatingMaterial.TiO2);
            AddMaterial(CoatingMaterial.Ta2O5);
            AddMaterial(CoatingMaterial.ZrO2);
            AddMaterial(CoatingMaterial.HfO2);
        }

        // Coating management
        public void Add(CoatingDefinition coating)
        {
            if (coating == null) throw new ArgumentNullException(nameof(coating));
            if (string.IsNullOrWhiteSpace(coating.Name))
                throw new ArgumentException("Coating must have a name", nameof(coating));
            _coatings[coating.Name] = coating;
        }

        public void Add(string name, CoatingDefinition coating)
        {
            if (coating == null) throw new ArgumentNullException(nameof(coating));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
            _coatings[name] = coating;
        }

        public CoatingDefinition? Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _coatings.TryGetValue(name, out var coating) ? coating : null;
        }

        public bool TryGet(string name, out CoatingDefinition? coating)
        {
            coating = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _coatings.TryGetValue(name, out coating);
        }

        public bool Contains(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _coatings.ContainsKey(name);
        }

        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _coatings.Remove(name);
        }

        public void Clear()
        {
            _coatings.Clear();
            _materials.Clear();
        }

        public int Count => _coatings.Count;
        public IEnumerable<string> Names => _coatings.Keys;
        public IEnumerable<CoatingDefinition> Coatings => _coatings.Values;

        // Factory methods
        public static CoatingCatalog Create() => new CoatingCatalog();

        public static CoatingCatalog CreateWithStandardCoatings(double referenceWavelength_um = 0.55)
        {
            var catalog = new CoatingCatalog();
            catalog.AddStandardMaterials();
            catalog.Add(CoatingDefinition.SingleLayerAR(referenceWavelength_um));
            catalog.Add(CoatingDefinition.VCoatAR(referenceWavelength_um));
            catalog.Add(CoatingDefinition.BroadbandAR(referenceWavelength_um));
            return catalog;
        }

        // File I/O
        public static CoatingCatalog LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Coating catalog file not found", filePath);

            var catalog = new CoatingCatalog();
            catalog.FilePath = filePath;

            string[] lines = File.ReadAllLines(filePath);
            CoatingDefinition? currentCoating = null;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                string line = lines[lineNum].Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (line.Equals("END", StringComparison.OrdinalIgnoreCase))
                    break;

                string[] tokens = SplitTokens(line);
                if (tokens.Length == 0) continue;

                string keyword = tokens[0].ToUpperInvariant();

                if (keyword == "MATERIAL")
                {
                    if (tokens.Length < 3)
                        throw new FormatException($"Line {lineNum + 1}: MATERIAL requires name and model");

                    string name = tokens[1];
                    string model = tokens[2].ToUpperInvariant();
                    CoatingMaterial material = ParseMaterial(name, model, tokens, lineNum);
                    catalog.AddMaterial(material);
                }
                else if (keyword == "COATING")
                {
                    if (tokens.Length < 3)
                        throw new FormatException($"Line {lineNum + 1}: COATING requires name and reference wavelength");

                    if (currentCoating != null)
                        catalog.Add(currentCoating);

                    string name = tokens[1];
                    double refWavelength = double.Parse(tokens[2], CultureInfo.InvariantCulture);

                    currentCoating = new CoatingDefinition
                    {
                        Name = name,
                        ReferenceWavelength_um = refWavelength,
                        Layers = new List<CoatingLayer>()
                    };
                }
                else if (keyword == "LAYER")
                {
                    if (currentCoating == null)
                        throw new FormatException($"Line {lineNum + 1}: LAYER must be inside a COATING block");

                    if (tokens.Length < 4)
                        throw new FormatException($"Line {lineNum + 1}: LAYER requires material, thickness, and type (O/P)");

                    string materialName = tokens[1];
                    double thickness = double.Parse(tokens[2], CultureInfo.InvariantCulture);
                    string thicknessType = tokens[3].ToUpperInvariant();

                    CoatingMaterial? material = catalog.GetMaterial(materialName);
                    if (material == null)
                        throw new FormatException($"Line {lineNum + 1}: Unknown material '{materialName}'");

                    bool isOptical = thicknessType == "O";
                    if (thicknessType != "O" && thicknessType != "P")
                        throw new FormatException($"Line {lineNum + 1}: Thickness type must be O (optical) or P (physical)");

                    currentCoating.Layers.Add(new CoatingLayer
                    {
                        Material = material,
                        Thickness = thickness,
                        IsOpticalThickness = isOptical
                    });
                }
            }

            if (currentCoating != null)
                catalog.Add(currentCoating);

            return catalog;
        }

        public void SaveToFile(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("# Coating Catalog File");
                writer.WriteLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteLine();

                writer.WriteLine("# === MATERIALS ===");
                writer.WriteLine();

                foreach (var material in _materials.Values)
                    WriteMaterial(writer, material);

                writer.WriteLine();
                writer.WriteLine("# === COATINGS ===");
                writer.WriteLine();

                foreach (var coating in _coatings.Values)
                {
                    WriteCoating(writer, coating);
                    writer.WriteLine();
                }

                writer.WriteLine("END");
            }

            FilePath = filePath;
        }

        private static string[] SplitTokens(string line)
        {
            var tokens = new List<string>();
            bool inQuotes = false;
            string current = "";

            foreach (char c in line)
            {
                if (c == '"')
                    inQuotes = !inQuotes;
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                }
                else
                    current += c;
            }

            if (current.Length > 0)
                tokens.Add(current);

            return tokens.ToArray();
        }

        private static CoatingMaterial ParseMaterial(string name, string model, string[] tokens, int lineNum)
        {
            var material = new CoatingMaterial { Name = name };

            switch (model)
            {
                case "CONSTANT":
                    if (tokens.Length < 4)
                        throw new FormatException($"Line {lineNum + 1}: CONSTANT material requires n value");
                    material.Model = DispersionModel.Constant;
                    material.RefractiveIndex = double.Parse(tokens[3], CultureInfo.InvariantCulture);
                    material.ExtinctionCoefficient = tokens.Length > 4
                        ? double.Parse(tokens[4], CultureInfo.InvariantCulture) : 0;
                    break;

                case "CAUCHY":
                    if (tokens.Length < 5)
                        throw new FormatException($"Line {lineNum + 1}: CAUCHY material requires A and B coefficients");
                    material.Model = DispersionModel.Cauchy;
                    material.Cauchy = new CauchyCoefficients
                    {
                        A = double.Parse(tokens[3], CultureInfo.InvariantCulture),
                        B = double.Parse(tokens[4], CultureInfo.InvariantCulture),
                        C = tokens.Length > 5 ? double.Parse(tokens[5], CultureInfo.InvariantCulture) : 0
                    };
                    material.RefractiveIndex = material.Cauchy.A;
                    break;

                case "SELLMEIER":
                    if (tokens.Length < 5)
                        throw new FormatException($"Line {lineNum + 1}: SELLMEIER material requires B1 and C1 coefficients");
                    material.Model = DispersionModel.Sellmeier;
                    material.Sellmeier = new SellmeierCoefficients
                    {
                        A = 1.0,
                        B1 = double.Parse(tokens[3], CultureInfo.InvariantCulture),
                        C1 = double.Parse(tokens[4], CultureInfo.InvariantCulture),
                        B2 = tokens.Length > 5 ? double.Parse(tokens[5], CultureInfo.InvariantCulture) : 0,
                        C2 = tokens.Length > 6 ? double.Parse(tokens[6], CultureInfo.InvariantCulture) : 0,
                        B3 = tokens.Length > 7 ? double.Parse(tokens[7], CultureInfo.InvariantCulture) : 0,
                        C3 = tokens.Length > 8 ? double.Parse(tokens[8], CultureInfo.InvariantCulture) : 0
                    };
                    material.RefractiveIndex = Math.Sqrt(material.Sellmeier.CalcNSquared(0.55));
                    break;

                default:
                    throw new FormatException($"Line {lineNum + 1}: Unknown material model '{model}'");
            }

            return material;
        }

        private static void WriteMaterial(StreamWriter writer, CoatingMaterial material)
        {
            switch (material.Model)
            {
                case DispersionModel.Constant:
                    if (material.ExtinctionCoefficient != 0)
                        writer.WriteLine($"MATERIAL {material.Name} CONSTANT {material.RefractiveIndex:G10} {material.ExtinctionCoefficient:G10}");
                    else
                        writer.WriteLine($"MATERIAL {material.Name} CONSTANT {material.RefractiveIndex:G10}");
                    break;

                case DispersionModel.Cauchy:
                    if (material.Cauchy.C != 0)
                        writer.WriteLine($"MATERIAL {material.Name} CAUCHY {material.Cauchy.A:G10} {material.Cauchy.B:G10} {material.Cauchy.C:G10}");
                    else
                        writer.WriteLine($"MATERIAL {material.Name} CAUCHY {material.Cauchy.A:G10} {material.Cauchy.B:G10}");
                    break;

                case DispersionModel.Sellmeier:
                case DispersionModel.SellmeierModified:
                    var s = material.Sellmeier;
                    if (s.B3 != 0 || s.C3 != 0)
                        writer.WriteLine($"MATERIAL {material.Name} SELLMEIER {s.B1:G10} {s.C1:G10} {s.B2:G10} {s.C2:G10} {s.B3:G10} {s.C3:G10}");
                    else if (s.B2 != 0 || s.C2 != 0)
                        writer.WriteLine($"MATERIAL {material.Name} SELLMEIER {s.B1:G10} {s.C1:G10} {s.B2:G10} {s.C2:G10}");
                    else
                        writer.WriteLine($"MATERIAL {material.Name} SELLMEIER {s.B1:G10} {s.C1:G10}");
                    break;

                case DispersionModel.Tabulated:
                    var (n, k) = material.GetNK(0.55);
                    writer.WriteLine($"MATERIAL {material.Name} CONSTANT {n:G10} {k:G10}");
                    break;
            }
        }

        private static void WriteCoating(StreamWriter writer, CoatingDefinition coating)
        {
            writer.WriteLine($"COATING {coating.Name} {coating.ReferenceWavelength_um:G10}");

            foreach (var layer in coating.Layers)
            {
                string thicknessType = layer.IsOpticalThickness ? "O" : "P";
                writer.WriteLine($"  LAYER {layer.Material.Name} {layer.Thickness:G10} {thicknessType}");
            }
        }

        public override string ToString()
        {
            return $"CoatingCatalog: {_materials.Count} materials, {_coatings.Count} coatings [{string.Join(", ", _coatings.Keys)}]";
        }
    }
}
