using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARCoatingDesigner.Core.Models;

namespace ARCoatingDesigner.Core.Catalogs
{
    /// <summary>
    /// Service for loading and managing glass and coating catalogs.
    /// </summary>
    public class CatalogService
    {
        private Dictionary<string, GlassData> _glassCatalog;
        private Dictionary<string, Dictionary<string, GlassData>> _glassByCatalog;
        private CoatingCatalog _coatingCatalog;

        public IReadOnlyDictionary<string, GlassData> GlassCatalog => _glassCatalog;
        public CoatingCatalog CoatingCatalog => _coatingCatalog;

        public IEnumerable<string> GlassNames => _glassCatalog.Keys.OrderBy(k => k);
        public IEnumerable<string> GlassCatalogNames => _glassByCatalog.Keys.OrderBy(k => k);

        public IEnumerable<string> GetGlassNamesFromCatalog(string catalogName)
        {
            if (_glassByCatalog == null || string.IsNullOrEmpty(catalogName))
                return Enumerable.Empty<string>();
            if (_glassByCatalog.TryGetValue(catalogName, out var glasses))
                return glasses.Keys.OrderBy(k => k);
            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> CoatingMaterialNames =>
            _coatingCatalog?.MaterialNames.OrderBy(n => n) ?? Enumerable.Empty<string>();

        public IEnumerable<string> CoatingNames =>
            _coatingCatalog?.Names.OrderBy(n => n) ?? Enumerable.Empty<string>();

        public string? GlassCatalogPath { get; private set; }
        public string? CoatingCatalogPath { get; private set; }

        public CatalogService()
        {
            _glassCatalog = new Dictionary<string, GlassData>(StringComparer.OrdinalIgnoreCase);
            _glassByCatalog = new Dictionary<string, Dictionary<string, GlassData>>(StringComparer.OrdinalIgnoreCase);
            _coatingCatalog = new CoatingCatalog();
        }

        /// <summary>
        /// Load glass catalog from AGF files in a directory.
        /// </summary>
        public void LoadGlassCatalog(string glassDataPath)
        {
            if (string.IsNullOrEmpty(glassDataPath) || !Directory.Exists(glassDataPath))
                throw new DirectoryNotFoundException($"Glass catalog directory not found: {glassDataPath}");

            var combined = new Dictionary<string, GlassData>(StringComparer.OrdinalIgnoreCase);
            var byCatalog = new Dictionary<string, Dictionary<string, GlassData>>(StringComparer.OrdinalIgnoreCase);

            foreach (var agfFile in Directory.GetFiles(glassDataPath, "*.AGF"))
            {
                string catalogName = Path.GetFileNameWithoutExtension(agfFile).ToUpperInvariant();
                var loaded = AgfParser.LoadAgfFile(agfFile);

                if (loaded.Count > 0)
                {
                    byCatalog[catalogName] = loaded;
                    foreach (var kvp in loaded)
                    {
                        if (!combined.ContainsKey(kvp.Key))
                            combined[kvp.Key] = kvp.Value;
                    }
                }
            }

            _glassCatalog = combined;
            _glassByCatalog = byCatalog;
            GlassCatalogPath = glassDataPath;
        }

        /// <summary>
        /// Load coating catalog from a .coat file.
        /// </summary>
        public void LoadCoatingCatalog(string coatingFilePath)
        {
            if (string.IsNullOrEmpty(coatingFilePath) || !File.Exists(coatingFilePath))
                throw new FileNotFoundException($"Coating catalog file not found: {coatingFilePath}");

            _coatingCatalog = CoatingCatalog.LoadFromFile(coatingFilePath);
            CoatingCatalogPath = coatingFilePath;
        }

        public void SaveCoatingCatalog(string coatingFilePath)
        {
            if (_coatingCatalog == null)
                throw new InvalidOperationException("No coating catalog loaded");

            _coatingCatalog.SaveToFile(coatingFilePath);
            CoatingCatalogPath = coatingFilePath;
        }

        public GlassData? GetGlass(string name)
        {
            if (_glassCatalog == null || string.IsNullOrEmpty(name))
                return null;
            return _glassCatalog.TryGetValue(name, out var glass) ? glass : null;
        }

        /// <summary>
        /// Get refractive index of a glass at a given wavelength.
        /// </summary>
        public double GetGlassIndex(string name, double wavelength_um)
        {
            var glass = GetGlass(name);
            if (glass == null)
                return 1.5168; // Default N-BK7 at 550nm
            return glass.CalculateN(wavelength_um);
        }

        public CoatingMaterial? GetCoatingMaterial(string name)
        {
            return _coatingCatalog?.GetMaterial(name);
        }

        public double GetCoatingMaterialIndex(string name, double wavelength_um)
        {
            var material = GetCoatingMaterial(name);
            if (material == null) return 1.5;
            var (n, k) = material.GetNK(wavelength_um);
            return n;
        }

        public CoatingDefinition? GetCoating(string name)
        {
            return _coatingCatalog?.Get(name);
        }

        public void AddCoatingMaterial(CoatingMaterial material)
        {
            _coatingCatalog ??= new CoatingCatalog();
            _coatingCatalog.AddMaterial(material);
        }

        public void AddCoating(CoatingDefinition coating)
        {
            _coatingCatalog ??= new CoatingCatalog();
            _coatingCatalog.Add(coating);
        }

        public bool RemoveCoating(string name)
        {
            return _coatingCatalog?.Remove(name) ?? false;
        }

        /// <summary>
        /// Try to find default catalog paths.
        /// </summary>
        public static (string? glassPath, string? coatingPath) FindDefaultCatalogPaths()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string coatFileName = "StandardCoatings.coat";

            // Directories that may contain AGF files directly
            string[] glassDirCandidates = { "catalogs/Glass", "catalogs", "GlassCat", "GlassData" };
            // Directories that may contain .coat files
            string[] coatDirCandidates = { "catalogs/Coatings", "catalogs", "Coatings" };

            string? foundGlass = null;
            string? foundCoating = null;

            string current = appDir;
            for (int i = 0; i < 10; i++)
            {
                // Look for a directory containing AGF files
                foreach (var dirName in glassDirCandidates)
                {
                    string candidate = Path.Combine(current, dirName);
                    if (Directory.Exists(candidate) &&
                        (Directory.GetFiles(candidate, "*.AGF", SearchOption.TopDirectoryOnly).Length > 0 ||
                         Directory.GetFiles(candidate, "*.agf", SearchOption.TopDirectoryOnly).Length > 0))
                    {
                        foundGlass ??= candidate;
                    }
                }

                // Look for coating file
                foreach (var dirName in coatDirCandidates)
                {
                    string candidate = Path.Combine(current, dirName, coatFileName);
                    if (File.Exists(candidate))
                        foundCoating ??= candidate;
                }

                if (foundGlass != null)
                    return (foundGlass, foundCoating);

                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            return (foundGlass, foundCoating);
        }

        /// <summary>
        /// Try to load default catalogs from standard locations.
        /// </summary>
        public bool TryLoadDefaultCatalogs()
        {
            var (glassPath, coatingPath) = FindDefaultCatalogPaths();

            bool glassLoaded = false;
            bool coatingLoaded = false;

            if (!string.IsNullOrEmpty(glassPath) && Directory.Exists(glassPath))
            {
                try { LoadGlassCatalog(glassPath); glassLoaded = true; }
                catch { /* ignore */ }
            }

            if (!string.IsNullOrEmpty(coatingPath) && File.Exists(coatingPath))
            {
                try { LoadCoatingCatalog(coatingPath); coatingLoaded = true; }
                catch { /* ignore */ }
            }

            if (!coatingLoaded)
            {
                _coatingCatalog = new CoatingCatalog();
                _coatingCatalog.AddStandardMaterials();
            }

            return glassLoaded;
        }

        /// <summary>
        /// Initialize with standard materials only (no file loading).
        /// </summary>
        public void InitializeWithStandardMaterials()
        {
            _coatingCatalog = new CoatingCatalog();
            _coatingCatalog.AddStandardMaterials();
        }
    }
}
