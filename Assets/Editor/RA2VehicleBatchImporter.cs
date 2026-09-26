using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public static class RA2VehicleBatchImporter
    {
        public sealed class Result
        {
            public int Total;
            public int Imported;
            public int Skipped;
            public int Failed;
            public readonly List<string> Messages = new List<string>();

            public override string ToString()
            {
                return
                    $"Vehicles total: {Total}\n" +
                    $"Imported: {Imported}\n" +
                    $"Skipped: {Skipped}\n" +
                    $"Failed: {Failed}";
            }
        }

        public static Result ImportAll()
        {
            var context = RA2GenericVehicleImporter.CreateContext();
            IniDocument rules = LoadMergedRulesIni(context.LocalRa2);

            if (rules == null)
                throw new InvalidOperationException("rules.ini / rulesmd.ini could not be loaded.");

            var vehicles = rules.GetSection("VehicleTypes");
            if (vehicles == null)
                throw new InvalidOperationException("[VehicleTypes] was not found in rules.ini/rulesmd.ini.");

            var result = new Result();

            foreach (var kv in vehicles)
            {
                string id = kv.Value?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                result.Total++;

                var entry = context.Database.Get(id);
                if (entry == null)
                {
                    result.Skipped++;
                    result.Messages.Add($"{id}: skipped (not resolved in object database)");
                    continue;
                }

                string imageBase = !string.IsNullOrWhiteSpace(entry.ImageId)
                    ? Path.GetFileNameWithoutExtension(entry.ImageId.Trim())
                    : id;

                string[] candidates =
                {
                    imageBase.ToUpperInvariant() + ".VXL",
                    id.ToUpperInvariant() + ".VXL"
                };

                bool hasVxl = false;
                foreach (string candidate in candidates)
                {
                    if (RA2AssetLocator.FindInDirectory(
                        context.LocalRa2,
                        candidate,
                        4
                    ) != null)
                    {
                        hasVxl = true;
                        break;
                    }
                }

                if (!hasVxl)
                {
                    result.Skipped++;
                    result.Messages.Add(
                        $"{id}: skipped (no VXL found: {string.Join(", ", candidates)})"
                    );
                    continue;
                }

                try
                {
                    RA2GenericVehicleImporter.Import(id, context);
                    result.Imported++;
                    result.Messages.Add($"{id}: imported");
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"{id}: FAILED - {ex.Message}");
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(result + "\n" + string.Join("\n", result.Messages));
            return result;
        }

        private static IniDocument LoadMergedRulesIni(string localRa2)
        {
            var parts = new List<string>();

            foreach (string filename in new[] { "rules.ini", "rulesmd.ini" })
            {
                var found = RA2AssetLocator.FindInDirectory(
                    localRa2,
                    filename,
                    4
                );

                if (found != null)
                    parts.Add(System.Text.Encoding.UTF8.GetString(found.Data));
            }

            return parts.Count == 0
                ? null
                : IniDocument.Parse(string.Join("\n", parts));
        }
    }
}
