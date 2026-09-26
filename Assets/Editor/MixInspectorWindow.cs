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
        private string status = "Choose a MIX archive from LocalRA2.";
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
                status =
                    $"Archive: {Path.GetFileName(selectedMix)}\n" +
                    $"Entries: {mix.EntryCount}\n" +
                    $"Encrypted: {mix.IsEncrypted}\n" +
                    $"Checksum flag: {mix.HasChecksum}";
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
                using var mix = new MixArchive(selectedMix);
                uint hash = WestwoodCrc32.HashFilename(filename);
                bool found = mix.Contains(filename);

                status =
                    $"Filename: {filename}\n" +
                    $"Westwood hash: 0x{hash:X8}\n" +
                    $"Found: {found}\n" +
                    $"Archive entries: {mix.EntryCount}";
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
                using var mix = new MixArchive(selectedMix);

                if (!mix.Contains(filename))
                {
                    status = $"'{filename}' is not present in this archive.";
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

                mix.Extract(filename, destination);
                AssetDatabase.Refresh();

                status = $"Extracted to:\n{destination}";
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
        }

        private bool ValidateSelection()
        {
            if (string.IsNullOrWhiteSpace(selectedMix) || !File.Exists(selectedMix))
            {
                status = "Choose a valid MIX archive first.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                status = "Enter a filename such as E2.SHP.";
                return false;
            }

            return true;
        }
    }
}
