using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RA2RPG.EditorTools
{
    public sealed class RA2ImportWindow : EditorWindow
    {
        private Vector2 scroll;
        private readonly List<string> mixFiles = new List<string>();

        [MenuItem("RA2 RPG/RA2 Asset Importer")]
        public static void Open()
        {
            GetWindow<RA2ImportWindow>("RA2 Asset Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Local Red Alert 2 files", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Put your own RA2/Yuri's Revenge files in the LocalRA2 folder at the project root. " +
                "This folder is ignored by Git. The first importer milestone scans the MIX archives; " +
                "SHP/TMP/PAL/VXL extraction will be added next.",
                MessageType.Info
            );

            if (GUILayout.Button("Scan LocalRA2"))
                Scan();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"MIX archives found: {mixFiles.Count}");

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string file in mixFiles)
                EditorGUILayout.SelectableLabel(file, GUILayout.Height(18));
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            mixFiles.Clear();

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "LocalRA2"));

            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
                Debug.Log($"Created LocalRA2 at {root}");
                return;
            }

            foreach (string file in Directory.GetFiles(root, "*.mix", SearchOption.AllDirectories))
                mixFiles.Add(file);

            mixFiles.Sort();
            Debug.Log($"RA2 importer found {mixFiles.Count} MIX archive(s).");
        }
    }
}
