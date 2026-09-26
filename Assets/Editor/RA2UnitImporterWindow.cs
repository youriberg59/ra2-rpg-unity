using System;
using UnityEditor;
using UnityEngine;

namespace RA2RPG.EditorTools
{
    public sealed class RA2UnitImporterWindow : EditorWindow
    {
        private string objectId = "E2";
        private string status = "Enter an RA2 object ID, for example E2.";

        [MenuItem("RA2 RPG/Import Unit")]
        public static void Open()
        {
            GetWindow<RA2UnitImporterWindow>("RA2 Unit Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RA2 Generic Unit Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            objectId = EditorGUILayout.TextField("Object ID", objectId);

            if (GUILayout.Button("Import Unit", GUILayout.Height(30)))
                ImportCurrent();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void ImportCurrent()
        {
            try
            {
                string prefabPath = RA2GenericUnitImporter.Import(objectId);
                status = $"Imported successfully.\nPrefab: {prefabPath}";

                UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            catch (Exception ex)
            {
                status = ex.Message;
                Debug.LogException(ex);
            }
        }
    }
}
