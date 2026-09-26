using System;
using UnityEditor;
using UnityEngine;

namespace RA2RPG.EditorTools
{
    public sealed class RA2VehicleImporterWindow : EditorWindow
    {
        private string objectId = "HTNK";
        private string status = "Enter a VXL vehicle ID, for example HTNK.";

        [MenuItem("RA2 RPG/Import Vehicle")]
        public static void Open()
        {
            GetWindow<RA2VehicleImporterWindow>("RA2 Vehicle Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RA2 VXL Vehicle Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            objectId = EditorGUILayout.TextField("Object ID", objectId);

            if (GUILayout.Button("Import Vehicle", GUILayout.Height(30)))
            {
                try
                {
                    string prefabPath = RA2GenericVehicleImporter.Import(objectId);
                    status = $"Imported successfully.\nPrefab: {prefabPath}";

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                catch (Exception ex)
                {
                    status = ex.Message;
                    Debug.LogException(ex);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(status, MessageType.Info);
        }
    }
}
