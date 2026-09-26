using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RA2RPG.Player;
using RA2RPG.CameraSystem;
using RA2RPG.Combat;

namespace RA2RPG.EditorTools
{
    public static class CreateDemoScene
    {
        [MenuItem("RA2 RPG/Create Demo Scene")]
        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateGrid();

            GameObject hero = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hero.name = "Hero";
            hero.transform.position = Vector3.zero;
            hero.transform.localScale = new Vector3(0.65f, 0.95f, 1f);

            var heroRenderer = hero.GetComponent<MeshRenderer>();
            heroRenderer.material = new Material(Shader.Find("Sprites/Default"));
            heroRenderer.material.color = new Color(0.95f, 0.75f, 0.15f);

            hero.AddComponent<HeroClickMover>();
            hero.AddComponent<HeroShooter>();

            GameObject cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.08f);
            cameraObject.tag = "MainCamera";

            var follow = cameraObject.AddComponent<HeroCameraFollow>();
            follow.SetTarget(hero.transform);

            string folder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, folder + "/RPGDemo.unity");
            Selection.activeGameObject = hero;

            Debug.Log("RA2 RPG demo created. Play, then click anywhere to move. Shift+click fires.");
        }

        private static void CreateGrid()
        {
            const int radius = 12;
            const float tileWidth = 1.6f;
            const float tileHeight = 0.8f;

            Transform root = new GameObject("Isometric Ground").transform;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.SetParent(root);

                    float px = (x - y) * tileWidth * 0.5f;
                    float py = (x + y) * tileHeight * 0.5f;
                    tile.transform.position = new Vector3(px, py, 1f);
                    tile.transform.localScale = new Vector3(tileWidth * 0.96f, tileHeight * 0.96f, 1f);

                    var renderer = tile.GetComponent<MeshRenderer>();
                    renderer.material = new Material(Shader.Find("Sprites/Default"));

                    bool alternate = ((x + y) & 1) == 0;
                    renderer.material.color = alternate
                        ? new Color(0.24f, 0.34f, 0.22f)
                        : new Color(0.20f, 0.30f, 0.19f);
                }
            }
        }
    }
}
