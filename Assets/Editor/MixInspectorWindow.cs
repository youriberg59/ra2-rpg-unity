using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public sealed class MixInspectorWindow : EditorWindow
    {
        private string selectedMix = "";
        private string filename = "E2.SHP";
        private string status = "Check filename searches every MIX in LocalRA2 automatically.";
        private Vector2 scroll;

        [MenuItem("RA2 RPG/MIX Inspector")]
        public static void Open()
        {
            GetWindow<MixInspectorWindow>("MIX Inspector");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Westwood MIX Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Archive", selectedMix);

                if (GUILayout.Button("Choose", GUILayout.Width(80)))
                {
                    string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LocalRA2"));
                    Directory.CreateDirectory(root);

                    string picked = EditorUtility.OpenFilePanel("Choose a MIX archive", root, "mix");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        selectedMix = picked;
                        InspectArchive();
                    }
                }
            }

            filename = EditorGUILayout.TextField("Known filename", filename);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check filename"))
                    CheckFilename();

                if (GUILayout.Button("Extract filename"))
                    ExtractFilename();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void InspectArchive()
        {
            if (string.IsNullOrEmpty(selectedMix))
                return;

            try
            {
                using var mix = new MixArchive(selectedMix);

                string[] knownContainers =
                {
                    "conquer.mix",
                    "local.mix",
                    "cache.mix",
                    "generic.mix",
                    "isogen.mix",
                    "neutral.mix",
                    "load.mix",
                    "temperat.mix",
                    "snow.mix",
                    "urban.mix"
                };

                var lines = new System.Collections.Generic.List<string>
                {
                    $"Archive: {Path.GetFileName(selectedMix)}",
                    $"Entries: {mix.EntryCount}",
                    $"Encrypted: {mix.IsEncrypted}",
                    $"Checksum flag: {mix.HasChecksum}",
                    "",
                    "Known nested containers:"
                };

                foreach (string name in knownContainers)
                {
                    uint hash = WestwoodCrc32.HashFilename(name);
                    lines.Add($"{name}  0x{hash:X8}  {(mix.Contains(name) ? "FOUND" : "-")}");
                }

                if (string.Equals(filename, "E2.SHP", StringComparison.OrdinalIgnoreCase))
                {
                    string[] aliases =
                    {
                        "E2.SHP",
                        "E2",
                        "CONS.SHP",
                        "CONSCRIPT.SHP",
                        "CONSCRIPT",
                        "CONSCRIP.SHP",
                        "CONSCRPT.SHP"
                    };

                    lines.Add("");
                    lines.Add("Conscript filename probes:");

                    foreach (string alias in aliases)
                    {
                        uint aliasHash = WestwoodCrc32.HashFilename(alias);
                        bool aliasFound = mix.Contains(alias);
                        lines.Add($"{alias}  0x{aliasHash:X8}  {(aliasFound ? "FOUND" : "-")}");
                    }
                }

                lines.Add("");
                lines.Add("Raw entry hashes:");
                foreach (var entry in mix.Entries)
                    lines.Add($"0x{entry.Hash:X8}  offset={entry.Offset}  length={entry.Length}");

                status = string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
        }

        private void CheckFilename()
        {
            if (!ValidateSelection())
                return;

            try
            {
                uint hash = WestwoodCrc32.HashFilename(filename);
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LocalRA2"));
                var trace = new RA2AssetLocator.SearchTrace();

                RA2AssetLocator.SearchResult result;
                if (string.Equals(filename, "E2.SHP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(filename, "E2", StringComparison.OrdinalIgnoreCase))
                {
                    result = RA2UnitAssetResolver.FindUnitSprite(root, "E2", 4, trace);
                }
                else
                {
                    result = RA2AssetLocator.FindInDirectory(root, filename, 4, trace);
                }

                bool found = result != null;

                int topLevelMixCount = 0;
                if (Directory.Exists(root))
                {
                    topLevelMixCount += Directory.GetFiles(root, "*.mix", SearchOption.AllDirectories).Length;
                    topLevelMixCount += Directory.GetFiles(root, "*.dat", SearchOption.AllDirectories).Length;
                }

                status =
                    $"Filename: {filename}\n" +
                    $"Westwood hash: 0x{hash:X8}\n" +
                    $"Found: {found}\n" +
                    $"MIX/DAT archives scanned: {topLevelMixCount}" +
                    (found ? $"\nPath: {result.Path}" : "") +
                    $"\n\nSearch trace:\n{trace}";
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
        }

        private void ExtractFilename()
        {
            if (!ValidateSelection())
                return;

            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LocalRA2"));

                RA2AssetLocator.SearchResult result;
                if (string.Equals(filename, "E2.SHP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(filename, "E2", StringComparison.OrdinalIgnoreCase))
                {
                    result = RA2UnitAssetResolver.FindUnitSprite(root, "E2", 4);
                }
                else
                {
                    result = RA2AssetLocator.FindInDirectory(root, filename, 4);
                }

                if (result == null)
                {
                    status = $"'{filename}' was not found in any MIX under LocalRA2.";
                    return;
                }

                string generatedRoot = Path.Combine(
                    Application.dataPath,
                    "Generated",
                    "RA2",
                    "Extracted"
                );

                Directory.CreateDirectory(generatedRoot);
                string destination = Path.Combine(generatedRoot, Path.GetFileName(filename));

                File.WriteAllBytes(destination, result.Data);
                AssetDatabase.Refresh();

                status = $"Found at:\n{result.Path}\n\nExtracted to:\n{destination}";
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
        }

        private bool ValidateSelection()
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                status = "Enter a filename such as E2.SHP.";
                return false;
            }

            return true;
        }
    }
}
