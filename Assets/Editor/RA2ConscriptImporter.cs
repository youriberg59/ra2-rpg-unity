using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;
using RA2RPG.Player;

namespace RA2RPG.EditorTools
{
    public static class RA2ConscriptImporter
    {
        private const string OutputFolder = "Assets/Generated/RA2/E2";
        private const string SpriteName = "CONS.SHP";
        private const string PaletteName = "unittem.pal";

        [MenuItem("RA2 RPG/Import E2 Conscript")]
        public static void Import()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string localRa2 = Path.Combine(projectRoot, "LocalRA2");

                var spriteResult = RA2AssetLocator.FindInDirectory(
                    localRa2,
                    SpriteName,
                    4
                );

                if (spriteResult == null)
                    throw new FileNotFoundException($"{SpriteName} was not found in LocalRA2 archives.");

                var paletteResult = RA2AssetLocator.FindInDirectory(
                    localRa2,
                    PaletteName,
                    4
                );

                if (paletteResult == null)
                    throw new FileNotFoundException($"{PaletteName} was not found in LocalRA2 archives.");

                var shp = ShpFileDecoder.Decode(spriteResult.Data);
                var palette = WestwoodPalette.FromBytes(paletteResult.Data);

                EnsureFolder("Assets", "Generated");
                EnsureFolder("Assets/Generated", "RA2");
                EnsureFolder("Assets/Generated/RA2", "E2");

                var generatedPaths = new List<string>();

                for (int i = 0; i < shp.Frames.Count; i++)
                {
                    string assetPath = $"{OutputFolder}/frame_{i:D3}.png";
                    WriteFramePng(shp, shp.Frames[i], palette, assetPath);
                    generatedPaths.Add(assetPath);
                }

                AssetDatabase.Refresh();

                foreach (string assetPath in generatedPaths)
                    ConfigureSpriteImporter(assetPath);

                AssetDatabase.Refresh();

                var sprites = new List<Sprite>();
                foreach (string assetPath in generatedPaths)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite != null)
                        sprites.Add(sprite);
                }

                if (sprites.Count == 0)
                    throw new InvalidOperationException("SHP decoded, but Unity did not import any generated sprites.");

                ApplyToHero(sprites.ToArray());

                Debug.Log(
                    $"Imported E2/Conscript: {sprites.Count} frames from {spriteResult.Path} " +
                    $"using {paletteResult.Path}."
                );
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog(
                    "RA2 Conscript Import",
                    ex.Message,
                    "OK"
                );
            }
        }

        private static void WriteFramePng(
            ShpFileDecoder shp,
            ShpFileDecoder.Frame frame,
            WestwoodPalette palette,
            string assetPath
        )
        {
            int width = Math.Max(1, shp.CanvasWidth);
            int height = Math.Max(1, shp.CanvasHeight);

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false
            )
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    int sourceIndex = y * frame.Width + x;
                    if (sourceIndex >= frame.Pixels.Length)
                        continue;

                    byte paletteIndex = frame.Pixels[sourceIndex];
                    if (paletteIndex == 0)
                        continue;

                    int canvasX = frame.X + x;
                    int canvasYTop = frame.Y + y;

                    if (canvasX < 0 || canvasX >= width ||
                        canvasYTop < 0 || canvasYTop >= height)
                        continue;

                    // SHP coordinates are top-down; Unity texture coordinates are bottom-up.
                    int canvasY = height - 1 - canvasYTop;
                    pixels[canvasY * width + canvasX] = palette.Colors[paletteIndex];
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            string absolute = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", assetPath)
            );

            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, png);
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 48f;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static void ApplyToHero(Sprite[] sprites)
        {
            GameObject hero = GameObject.Find("Hero");
            if (hero == null)
                throw new InvalidOperationException(
                    "Hero object not found. Create/open the RPGDemo scene first."
                );

            var meshRenderer = hero.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                UnityEngine.Object.DestroyImmediate(meshRenderer);

            var meshFilter = hero.GetComponent<MeshFilter>();
            if (meshFilter != null)
                UnityEngine.Object.DestroyImmediate(meshFilter);

            var meshCollider = hero.GetComponent<MeshCollider>();
            if (meshCollider != null)
                UnityEngine.Object.DestroyImmediate(meshCollider);

            var spriteRenderer = hero.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = hero.AddComponent<SpriteRenderer>();

            spriteRenderer.sprite = sprites[0];
            spriteRenderer.sortingOrder = 100;
            hero.transform.localScale = Vector3.one;

            var animator = hero.GetComponent<SimpleSpriteAnimator>();
            if (animator == null)
                animator = hero.AddComponent<SimpleSpriteAnimator>();

            // Use a small subset for the visual validation pass. Proper RA2 action
            // sequence mapping (standing/walking/firing/death + facings) comes next.
            int count = Math.Min(8, sprites.Length);
            var preview = new Sprite[count];
            Array.Copy(sprites, preview, count);
            animator.SetFrames(preview, 8f);

            EditorUtility.SetDirty(hero);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
