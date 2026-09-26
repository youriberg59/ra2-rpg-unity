using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public static class RA2InfantryBatchImporter
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
                    $"Infantry total: {Total}\n" +
                    $"Imported: {Imported}\n" +
                    $"Skipped: {Skipped}\n" +
                    $"Failed: {Failed}";
            }
        }

        public static Result ImportAll()
        {
            var context = RA2GenericUnitImporter.CreateContext();
            IniDocument rules = LoadMergedRulesIni(context.LocalRa2);

            if (rules == null)
                throw new InvalidOperationException("rules.ini / rulesmd.ini could not be loaded.");

            var infantry = rules.GetSection("InfantryTypes");
            if (infantry == null)
                throw new InvalidOperationException("[InfantryTypes] was not found in rules.ini/rulesmd.ini.");

            var result = new Result();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var kv in infantry)
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

                    if (string.IsNullOrWhiteSpace(entry.SpriteFilename) ||
                        !entry.SpriteFilename.EndsWith(".SHP", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Skipped++;
                        result.Messages.Add(
                            $"{id}: skipped ({entry.SpriteFilename ?? "no sprite"}; SHP only)"
                        );
                        continue;
                    }

                    if (!entry.SpriteFound)
                    {
                        result.Skipped++;
                        result.Messages.Add(
                            $"{id}: skipped ({entry.SpriteFilename} not found in MIX archives)"
                        );
                        continue;
                    }

                    try
                    {
                        RA2GenericUnitImporter.Import(id, context);
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
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log(result + "\n" + string.Join("\n", result.Messages));
            return result;
        }

        private static IniDocument LoadMergedRulesIni(string localRa2)
        {
            var parts = new List<string>();

            foreach (string filename in new[] { "rules.ini", "rulesmd.ini" })
            {
                var found = RA2AssetLocator.FindInDirectory(localRa2, filename, 4);
                if (found != null)
                    parts.Add(System.Text.Encoding.UTF8.GetString(found.Data));
            }

            return parts.Count == 0
                ? null
                : IniDocument.Parse(string.Join("\n", parts));
        }
    }
}
