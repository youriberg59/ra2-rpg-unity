using System;
using System.Collections.Generic;

namespace RA2RPG.RA2
{
    public static class RA2UnitAssetResolver
    {
        private static readonly Dictionary<string, string[]> KnownSpriteCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Retail RA2: rules unit id E2 (Conscript) uses CONS.SHP.
                ["E2"] = new[] { "CONS.SHP", "E2.SHP" },

                // Common defaults / fallbacks for future expansion.
                ["E1"] = new[] { "E1.SHP", "GI.SHP" },
                ["FLAKT"] = new[] { "FLAKT.SHP" },
                ["ENGINEER"] = new[] { "ENGINEER.SHP", "ENGI.SHP" },
                ["SPY"] = new[] { "SPY.SHP" },
                ["TANY"] = new[] { "TANY.SHP", "TANYA.SHP" },
                ["BORIS"] = new[] { "BORIS.SHP" }
            };

        public static string[] GetSpriteCandidates(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return Array.Empty<string>();

            if (KnownSpriteCandidates.TryGetValue(unitId.Trim(), out string[] candidates))
                return candidates;

            string id = unitId.Trim().ToUpperInvariant();
            return new[] { id + ".SHP" };
        }

        public static RA2AssetLocator.SearchResult FindUnitSprite(
            string localRa2Directory,
            string unitId,
            int maxDepth = 4,
            RA2AssetLocator.SearchTrace trace = null
        )
        {
            foreach (string candidate in GetSpriteCandidates(unitId))
            {
                trace?.Add($"UNIT {unitId} -> trying {candidate}");

                var result = RA2AssetLocator.FindInDirectory(
                    localRa2Directory,
                    candidate,
                    maxDepth,
                    trace
                );

                if (result != null)
                {
                    trace?.Add($"UNIT {unitId} -> resolved as {candidate}");
                    return result;
                }
            }

            trace?.Add($"UNIT {unitId} -> no sprite candidate found");
            return null;
        }
    }
}
