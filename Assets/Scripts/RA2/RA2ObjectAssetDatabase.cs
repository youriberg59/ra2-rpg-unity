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
            public string SequenceId;
            public bool IsVoxel;
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

            trace?.Add("Building global RA2 archive index...");
            RA2ArchiveIndex archiveIndex = RA2ArchiveIndex.Build(
                localRa2Directory,
                trace,
                3
            );
            trace?.Add(
                $"Archive index ready: {archiveIndex.OpenedArchives} archives, " +
                $"{archiveIndex.IndexedEntries} entries, {archiveIndex.UniqueHashes} unique hashes."
            );

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

                string sequenceId =
                    art?.Get(imageId, "Sequence") ??
                    art?.Get(id, "Sequence") ??
                    rules?.Get(id, "Sequence");

                bool isVoxel = IsTruthy(
                    art?.Get(imageId, "Voxel") ??
                    art?.Get(id, "Voxel") ??
                    rules?.Get(id, "Voxel")
                );

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
                    SpriteFilename = NormalizeSpriteFilename(imageId, isVoxel),
                    CameoFilename = NormalizeCameoFilename(cameoId),
                    AltCameoFilename = NormalizeCameoFilename(altCameoId),
                    SequenceId = sequenceId,
                    IsVoxel = isVoxel
                };

                ResolveEntry(archiveIndex, entry);
                db.entries[id] = entry;
            }

            return db;
        }

        private static void ResolveEntry(
            RA2ArchiveIndex archiveIndex,
            Entry entry
        )
        {
            if (!string.IsNullOrWhiteSpace(entry.SpriteFilename))
            {
                string spritePath = archiveIndex.FindPath(entry.SpriteFilename);
                if (spritePath != null)
                {
                    entry.SpriteFound = true;
                    entry.SpritePath = spritePath;
                }
            }

            string cameoFilename =
                entry.CameoFilename ??
                entry.AltCameoFilename;

            if (!string.IsNullOrWhiteSpace(cameoFilename))
            {
                string cameoPath = archiveIndex.FindPath(cameoFilename);
                if (cameoPath != null)
                {
                    entry.CameoFound = true;
                    entry.CameoPath = cameoPath;
                }
            }
        }

        private static IniDocument LoadMergedIni(
            string localRa2Directory,
            IEnumerable<string> filenames,
            RA2AssetLocator.SearchTrace trace
        )
        {
            var textParts = new List<string>();

            foreach (string filename in filenames)
            {
                var result = RA2AssetLocator.FindInDirectory(
                    localRa2Directory,
                    filename,
                    4,
                    trace
                );

                if (result != null)
                    textParts.Add(System.Text.Encoding.UTF8.GetString(result.Data));
            }

            return textParts.Count == 0
                ? null
                : IniDocument.Parse(string.Join("\n", textParts));
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSpriteFilename(string imageId, bool isVoxel)
        {
            if (string.IsNullOrWhiteSpace(imageId))
                return null;

            string value = imageId.Trim();

            if (value.EndsWith(".SHP", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".VXL", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".HVA", StringComparison.OrdinalIgnoreCase))
                return value.ToUpperInvariant();

            return value.ToUpperInvariant() + (isVoxel ? ".VXL" : ".SHP");
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
