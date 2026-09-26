using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public static class RA2GenericUnitImporter
    {
        public sealed class ImportContext
        {
            public string LocalRa2;
            public RA2ObjectAssetDatabase Database;
            public IniDocument Art;
            public WestwoodPalette Palette;
        }

        public static ImportContext CreateContext()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string localRa2 = Path.Combine(projectRoot, "LocalRA2");

            var paletteResult = RA2AssetLocator.FindInDirectory(
                localRa2,
                "unittem.pal",
                4
            );

            if (paletteResult == null)
                throw new FileNotFoundException("unittem.pal was not found in LocalRA2 archives.");

            return new ImportContext
            {
                LocalRa2 = localRa2,
                Database = RA2ObjectAssetDatabase.Build(localRa2, null),
                Art = LoadMergedArtIni(localRa2),
                Palette = WestwoodPalette.FromBytes(paletteResult.Data)
            };
        }

        public static string Import(string objectId)
        {
            return Import(objectId, CreateContext());
        }

        public static string Import(string objectId, ImportContext context)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentException("Object ID is required.", nameof(objectId));

            objectId = objectId.Trim().ToUpperInvariant();

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            string localRa2 = context.LocalRa2;
            var entry = context.Database.Get(objectId);

            if (entry == null)
                throw new InvalidOperationException($"RA2 object '{objectId}' was not found in rules/art.");

            if (string.IsNullOrWhiteSpace(entry.SpriteFilename))
                throw new InvalidOperationException($"'{objectId}' has no resolved sprite filename.");

            if (!entry.SpriteFilename.EndsWith(".SHP", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    $"'{objectId}' resolves to {entry.SpriteFilename}. " +
                    "The generic importer currently supports SHP units only; VXL/HVA support comes next."
                );

            var spriteResult = RA2AssetLocator.FindInDirectory(
                localRa2,
                entry.SpriteFilename,
                4
            );

            if (spriteResult == null)
                throw new FileNotFoundException(
                    $"Resolved sprite '{entry.SpriteFilename}' was not found in LocalRA2."
                );

            var shp = ShpFileDecoder.Decode(spriteResult.Data);
            var palette = context.Palette;

            string baseFolder = $"Assets/Generated/RA2/Units/{objectId}";
            string framesFolder = baseFolder + "/Frames";
            string prefabFolder = baseFolder + "/Prefabs";

            EnsureFolder("Assets", "Generated");
            EnsureFolder("Assets/Generated", "RA2");
            EnsureFolder("Assets/Generated/RA2", "Units");
            EnsureFolder("Assets/Generated/RA2/Units", objectId);
            EnsureFolder(baseFolder, "Frames");
            EnsureFolder(baseFolder, "Prefabs");

            var generatedPaths = new List<string>();
            var generatedPivots = new List<Vector2>();

            for (int i = 0; i < shp.Frames.Count; i++)
            {
                string assetPath = $"{framesFolder}/frame_{i:D3}.png";
                Vector2 pivot = WriteFramePng(shp, shp.Frames[i], palette, assetPath);
                generatedPaths.Add(assetPath);
                generatedPivots.Add(pivot);
            }

            AssetDatabase.Refresh();

            for (int i = 0; i < generatedPaths.Count; i++)
                ConfigureSpriteImporter(generatedPaths[i], generatedPivots[i]);

            AssetDatabase.Refresh();

            var sprites = new List<Sprite>();
            foreach (string assetPath in generatedPaths)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                    sprites.Add(sprite);
            }

            if (sprites.Count == 0)
                throw new InvalidOperationException(
                    $"'{objectId}' decoded successfully but Unity imported no sprites."
                );

            IniDocument art = context.Art;
            string resolvedSequenceId = ResolveSequenceId(entry, art);

            string prefabPath = $"{prefabFolder}/{objectId}.prefab";
            CreateOrReplacePrefab(
                objectId,
                entry.DisplayName,
                resolvedSequenceId,
                art,
                sprites.ToArray(),
                prefabPath
            );

            Debug.Log(
                $"RA2 generic import complete: {objectId} -> {entry.SpriteFilename}, " +
                $"{sprites.Count} frames, sequence: {resolvedSequenceId ?? "(none)"}, " +
                $"prefab: {prefabPath}"
            );

            return prefabPath;
        }

        private static Vector2 WriteFramePng(
            ShpFileDecoder shp,
            ShpFileDecoder.Frame frame,
            WestwoodPalette palette,
            string assetPath
        )
        {
            int width = Math.Max(1, shp.CanvasWidth);
            int height = Math.Max(1, shp.CanvasHeight);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            int lowestOpaqueY = height;

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

                    int canvasY = height - 1 - canvasYTop;
                    pixels[canvasY * width + canvasX] = palette.Colors[paletteIndex];

                    if (canvasY < lowestOpaqueY)
                        lowestOpaqueY = canvasY;
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

            float pivotY = lowestOpaqueY < height
                ? Mathf.Clamp01(lowestOpaqueY / (float)Math.Max(1, height - 1))
                : 0.5f;

            return new Vector2(0.5f, pivotY);
        }

        private static void ConfigureSpriteImporter(string assetPath, Vector2 pivot)
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
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static string ResolveSequenceId(
            RA2ObjectAssetDatabase.Entry entry,
            IniDocument art
        )
        {
            if (art == null || entry == null)
                return null;

            // Prefer the sequence already resolved by the catalog.
            if (!string.IsNullOrWhiteSpace(entry.SequenceId) &&
                art.HasSection(entry.SequenceId))
            {
                return entry.SequenceId;
            }

            // Retail RA2 normally stores Sequence= on the art section named after
            // the image ID (for example ENGINEER -> EngineerSequence).
            string[] sectionCandidates =
            {
                entry.ImageId,
                entry.Id,
                string.IsNullOrWhiteSpace(entry.SpriteFilename)
                    ? null
                    : Path.GetFileNameWithoutExtension(entry.SpriteFilename)
            };

            foreach (string section in sectionCandidates)
            {
                if (string.IsNullOrWhiteSpace(section))
                    continue;

                string sequence = art.Get(section, "Sequence");
                if (!string.IsNullOrWhiteSpace(sequence) && art.HasSection(sequence))
                    return sequence;
            }

            return null;
        }

        private static void CreateOrReplacePrefab(
            string objectId,
            string displayName,
            string sequenceId,
            IniDocument art,
            Sprite[] sprites,
            string prefabPath
        )
        {
            var root = new GameObject(objectId);
            root.name = objectId;

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites.Length > 0 ? sprites[0] : null;
            renderer.sortingOrder = 100;

            var metadata = root.AddComponent<RA2ImportedUnitMetadata>();
            metadata.ObjectId = objectId;
            metadata.DisplayName = displayName;

            if (!string.IsNullOrWhiteSpace(sequenceId) &&
                art != null &&
                art.HasSection(sequenceId))
            {
                var data = root.AddComponent<RA2InfantryAnimationData>();
                data.SequenceId = sequenceId;
                data.Ready = ParseSequenceRange(art.Get(sequenceId, "Ready"));
                data.Walk = ParseSequenceRange(art.Get(sequenceId, "Walk"));
                data.FireUp = ParseSequenceRange(art.Get(sequenceId, "FireUp"));
                data.FireProne = ParseSequenceRange(art.Get(sequenceId, "FireProne"));
                data.Die1 = ParseSequenceRange(art.Get(sequenceId, "Die1"));
                data.Die2 = ParseSequenceRange(art.Get(sequenceId, "Die2"));
                data.Idle1 = ParseSequenceRange(art.Get(sequenceId, "Idle1"));
                data.Idle2 = ParseSequenceRange(art.Get(sequenceId, "Idle2"));

                var animator = root.AddComponent<RA2GenericInfantryAnimator>();
                animator.Frames = sprites;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static IniDocument LoadMergedArtIni(string localRa2)
        {
            var parts = new List<string>();

            foreach (string filename in new[] { "art.ini", "artmd.ini" })
            {
                var result = RA2AssetLocator.FindInDirectory(localRa2, filename, 4);
                if (result != null)
                    parts.Add(System.Text.Encoding.UTF8.GetString(result.Data));
            }

            return parts.Count == 0
                ? null
                : IniDocument.Parse(string.Join("\n", parts));
        }

        private static RA2SequenceRange ParseSequenceRange(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            string[] parts = value.Split(',');
            if (parts.Length < 2)
                return default;

            if (!int.TryParse(parts[0].Trim(), out int start) ||
                !int.TryParse(parts[1].Trim(), out int frames))
                return default;

            int facingStride = frames;
            if (parts.Length >= 3 &&
                int.TryParse(parts[2].Trim(), out int parsedStride) &&
                parsedStride > 0)
            {
                facingStride = parsedStride;
            }

            return new RA2SequenceRange
            {
                Start = start,
                Frames = frames,
                FacingStride = facingStride
            };
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
