using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RA2RPG.RA2
{
    public sealed class RA2ObjectAssetDatabase
    {
        public sealed class Entry
        {
            public string Id;
            public string DisplayName;
            public string ImageId;
            public string SpriteFilename;
            public string CameoId;
            public string CameoFilename;
            public string AltCameoId;
            public string AltCameoFilename;
            public string SpritePath;
            public string CameoPath;
            public bool SpriteFound;
            public bool CameoFound;
        }

        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<Entry> Entries => entries.Values;

        public Entry Get(string id)
        {
            entries.TryGetValue(id, out var value);
            return value;
        }

        public static RA2ObjectAssetDatabase Build(string localRa2Directory, RA2AssetLocator.SearchTrace trace = null)
        {
            if (string.IsNullOrWhiteSpace(localRa2Directory) || !Directory.Exists(localRa2Directory))
                throw new DirectoryNotFoundException(localRa2Directory);

            var db = new RA2ObjectAssetDatabase();

            IniDocument rules = LoadMergedIni(
                localRa2Directory,
                new[] { "rules.ini", "rulesmd.ini" },
                trace
            );

            IniDocument art = LoadMergedIni(
                localRa2Directory,
                new[] { "art.ini", "artmd.ini" },
                trace
            );

            var objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string listSection in new[]
            {
                "InfantryTypes",
                "VehicleTypes",
                "AircraftTypes",
                "BuildingTypes"
            })
            {
                var section = rules?.GetSection(listSection);
                if (section == null)
                    continue;

                foreach (var kv in section)
                {
                    string id = kv.Value?.Trim();
                    if (!string.IsNullOrEmpty(id))
                        objectIds.Add(id);
                }
            }

            // Also include any sections that appear in art.ini even if they are omitted
            // from the type arrays, useful for custom/modded assets.
            if (art != null)
            {
                foreach (string section in art.SectionNames)
                {
                    if (!section.Contains("Types", StringComparison.OrdinalIgnoreCase))
                        objectIds.Add(section);
                }
            }

            foreach (string id in objectIds)
            {
                string imageId =
                    art?.Get(id, "Image") ??
                    rules?.Get(id, "Image") ??
                    id;

                string cameoId =
                    art?.Get(id, "Cameo") ??
                    rules?.Get(id, "Cameo");

                string altCameoId =
                    art?.Get(id, "AltCameo") ??
                    rules?.Get(id, "AltCameo");

                string displayName =
                    rules?.Get(id, "Name") ??
                    id;

                var entry = new Entry
                {
                    Id = id,
                    DisplayName = displayName,
                    ImageId = imageId,
                    CameoId = cameoId,
                    AltCameoId = altCameoId,
                    SpriteFilename = NormalizeSpriteFilename(imageId),
                    CameoFilename = NormalizeCameoFilename(cameoId),
                    AltCameoFilename = NormalizeCameoFilename(altCameoId)
                };

                ResolveEntry(localRa2Directory, entry, trace);
                db.entries[id] = entry;
            }

            return db;
        }

        private static void ResolveEntry(
            string localRa2Directory,
            Entry entry,
            RA2AssetLocator.SearchTrace trace
        )
        {
            if (!string.IsNullOrWhiteSpace(entry.SpriteFilename))
            {
                var sprite = RA2AssetLocator.FindInDirectory(
                    localRa2Directory,
                    entry.SpriteFilename,
                    4,
                    trace
                );

                if (sprite != null)
                {
                    entry.SpriteFound = true;
                    entry.SpritePath = sprite.Path;
                }
            }

            string cameoFilename =
                entry.CameoFilename ??
                entry.AltCameoFilename;

            if (!string.IsNullOrWhiteSpace(cameoFilename))
            {
                var cameo = RA2AssetLocator.FindInDirectory(
                    localRa2Directory,
                    cameoFilename,
                    4,
                    trace
                );

                if (cameo != null)
                {
                    entry.CameoFound = true;
                    entry.CameoPath = cameo.Path;
                }
            }
        }

        private static IniDocument LoadMergedIni(
            string localRa2Directory,
            IEnumerable<string> filenames,
            RA2AssetLocator.SearchTrace trace
        )
        {
            // Later files (e.g. rulesmd.ini) override earlier ones.
            IniDocument merged = new IniDocument();
            bool any = false;

            foreach (string filename in filenames)
            {
                var result = RA2AssetLocator.FindInDirectory(
                    localRa2Directory,
                    filename,
                    4,
                    trace
                );

                if (result == null)
                    continue;

                any = true;
                var doc = IniDocument.Parse(result.Data);

                foreach (string sectionName in doc.SectionNames)
                {
                    var section = doc.GetSection(sectionName);
                    if (section == null)
                        continue;

                    // Re-serialize this small section into a temporary fragment and
                    // merge through Parse to preserve case-insensitive behavior.
                    var lines = new List<string> { $"[{sectionName}]" };
                    lines.AddRange(section.Select(kv => $"{kv.Key}={kv.Value}"));
                    var fragment = IniDocument.Parse(string.Join("\n", lines));

                    var fragmentSection = fragment.GetSection(sectionName);
                    foreach (var kv in fragmentSection)
                    {
                        // IniDocument does not expose mutation, so merge into a text buffer later.
                    }
                }
            }

            if (!any)
                return null;

            // Rebuild by concatenating actual source files in precedence order.
            var textParts = new List<string>();
            foreach (string filename in filenames)
            {
                var result = RA2AssetLocator.FindInDirectory(localRa2Directory, filename, 4, trace);
                if (result != null)
                    textParts.Add(System.Text.Encoding.UTF8.GetString(result.Data));
            }

            return IniDocument.Parse(string.Join("\n", textParts));
        }

        private static string NormalizeSpriteFilename(string imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId))
                return null;

            string value = imageId.Trim();

            if (value.EndsWith(".SHP", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".VXL", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".HVA", StringComparison.OrdinalIgnoreCase))
                return value.ToUpperInvariant();

            return value.ToUpperInvariant() + ".SHP";
        }

        private static string NormalizeCameoFilename(string cameoId)
        {
            if (string.IsNullOrWhiteSpace(cameoId))
                return null;

            string value = cameoId.Trim();

            if (value.EndsWith(".SHP", StringComparison.OrdinalIgnoreCase))
                return value.ToUpperInvariant();

            return value.ToUpperInvariant() + ".SHP";
        }
    }
}
