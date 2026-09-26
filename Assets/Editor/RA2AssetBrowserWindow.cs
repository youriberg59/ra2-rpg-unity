using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public sealed class RA2AssetBrowserWindow : EditorWindow
    {
        private RA2ObjectAssetDatabase database;
        private Vector2 scroll;
        private string filter = "";
        private string status = "Build the asset index to resolve RA2 object IDs automatically.";

        [MenuItem("RA2 RPG/RA2 Asset Browser")]
        public static void Open()
        {
            GetWindow<RA2AssetBrowserWindow>("RA2 Asset Browser");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RA2 Automatic Asset Resolver", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Build asset index", GUILayout.Height(28)))
                BuildDatabase();

            filter = EditorGUILayout.TextField("Filter", filter ?? "");

            EditorGUILayout.HelpBox(status, MessageType.Info);

            if (database == null)
                return;

            var entries = database.Entries
                .Where(e =>
                    string.IsNullOrWhiteSpace(filter) ||
                    e.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.ImageId?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                .OrderBy(e => e.Id)
                .ToList();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var entry in entries)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField(
                        $"{entry.Id}  —  {entry.DisplayName}",
                        EditorStyles.boldLabel
                    );

                    EditorGUILayout.LabelField(
                        "Image",
                        $"{entry.ImageId ?? "-"}  →  {entry.SpriteFilename ?? "-"}"
                    );

                    EditorGUILayout.LabelField(
                        "Sprite",
                        entry.SpriteFound ? $"FOUND  {entry.SpritePath}" : "Not found"
                    );

                    if (!string.IsNullOrWhiteSpace(entry.CameoId))
                    {
                        EditorGUILayout.LabelField(
                            "Cameo",
                            $"{entry.CameoId}  →  {entry.CameoFilename ?? "-"}"
                        );

                        EditorGUILayout.LabelField(
                            "Cameo asset",
                            entry.CameoFound ? $"FOUND  {entry.CameoPath}" : "Not found"
                        );
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void BuildDatabase()
        {
            try
            {
                string root = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "LocalRA2")
                );

                var trace = new RA2AssetLocator.SearchTrace();
                database = RA2ObjectAssetDatabase.Build(root, trace);

                int total = database.Entries.Count;
                int sprites = database.Entries.Count(e => e.SpriteFound);
                int cameos = database.Entries.Count(e => e.CameoFound);

                status =
                    $"Objects resolved: {total}\n" +
                    $"Sprites found: {sprites}\n" +
                    $"Cameos found: {cameos}";
            }
            catch (Exception ex)
            {
                status = ex.ToString();
                database = null;
            }
        }
    }
}
