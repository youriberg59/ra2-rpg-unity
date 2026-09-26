using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public static class RA2GenericVehicleImporter
    {
        public static string Import(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                throw new ArgumentException("Vehicle object ID is required.", nameof(objectId));

            objectId = objectId.Trim().ToUpperInvariant();

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string localRa2 = Path.Combine(projectRoot, "LocalRA2");

            var database = RA2ObjectAssetDatabase.Build(localRa2, null);
            var entry = database.Get(objectId);

            if (entry == null)
                throw new InvalidOperationException($"RA2 object '{objectId}' was not found.");

            if (!entry.IsVoxel ||
                string.IsNullOrWhiteSpace(entry.SpriteFilename) ||
                !entry.SpriteFilename.EndsWith(".VXL", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{objectId}' is not resolved as a VXL vehicle. " +
                    $"Resolved asset: {entry.SpriteFilename ?? "(none)"}."
                );
            }

            var vxlResult = RA2AssetLocator.FindInDirectory(
                localRa2,
                entry.SpriteFilename,
                4
            );

            if (vxlResult == null)
                throw new FileNotFoundException(
                    $"{entry.SpriteFilename} was not found in LocalRA2 archives."
                );

            var paletteResult = RA2AssetLocator.FindInDirectory(
                localRa2,
                "unittem.pal",
                4
            );

            if (paletteResult == null)
                throw new FileNotFoundException("unittem.pal was not found.");

            var model = VxlFileDecoder.Decode(vxlResult.Data);
            var palette = WestwoodPalette.FromBytes(paletteResult.Data);

            string baseFolder = $"Assets/Generated/RA2/Vehicles/{objectId}";
            string meshFolder = baseFolder + "/Meshes";
            string prefabFolder = baseFolder + "/Prefabs";

            EnsureFolder("Assets", "Generated");
            EnsureFolder("Assets/Generated", "RA2");
            EnsureFolder("Assets/Generated/RA2", "Vehicles");
            EnsureFolder("Assets/Generated/RA2/Vehicles", objectId);
            EnsureFolder(baseFolder, "Meshes");
            EnsureFolder(baseFolder, "Prefabs");
            EnsureFolder("Assets/Generated/RA2", "Materials");

            Material material = GetOrCreateVertexColorMaterial();

            var root = new GameObject(objectId);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // A light isometric presentation for the current front-facing RPG camera.
            visual.transform.localRotation = Quaternion.Euler(28f, 45f, 0f);

            int totalVoxels = 0;
            var createdMeshes = new List<Mesh>();

            for (int i = 0; i < model.Limbs.Count; i++)
            {
                var limb = model.Limbs[i];
                totalVoxels += limb.Voxels.Count;

                Mesh mesh = BuildMesh(limb, palette);
                mesh.name = $"{objectId}_{Sanitize(limb.Name)}";

                string meshPath =
                    $"{meshFolder}/{objectId}_{i:D2}_{Sanitize(limb.Name)}.asset";

                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                    AssetDatabase.DeleteAsset(meshPath);

                AssetDatabase.CreateAsset(mesh, meshPath);
                createdMeshes.Add(mesh);

                var limbObject = new GameObject(
                    string.IsNullOrWhiteSpace(limb.Name) ? $"Limb_{i}" : limb.Name
                );

                limbObject.transform.SetParent(visual.transform, false);

                var filter = limbObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                var renderer = limbObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
            }

            NormalizeVisualScale(visual, 1.25f);

            var metadata = root.AddComponent<RA2ImportedVehicleMetadata>();
            metadata.ObjectId = objectId;
            metadata.DisplayName = entry.DisplayName;
            metadata.VxlFilename = entry.SpriteFilename;
            metadata.HvaFilename = Path.GetFileNameWithoutExtension(entry.SpriteFilename) + ".HVA";
            metadata.LimbCount = model.Limbs.Count;
            metadata.VoxelCount = totalVoxels;

            string prefabPath = $"{prefabFolder}/{objectId}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"RA2 vehicle imported: {objectId} -> {entry.SpriteFilename}; " +
                $"{model.Limbs.Count} limb(s), {totalVoxels} voxels; prefab: {prefabPath}"
            );

            return prefabPath;
        }

        private static Mesh BuildMesh(
            VxlFileDecoder.Limb limb,
            WestwoodPalette palette
        )
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color32>();

            float minX = limb.Bounds[0];
            float minY = limb.Bounds[1];
            float minZ = limb.Bounds[2];
            float maxX = limb.Bounds[3];
            float maxY = limb.Bounds[4];
            float maxZ = limb.Bounds[5];

            float sx = Math.Abs(maxX - minX) / Math.Max(1, limb.SizeX);
            float sy = Math.Abs(maxY - minY) / Math.Max(1, limb.SizeY);
            float sz = Math.Abs(maxZ - minZ) / Math.Max(1, limb.SizeZ);

            if (sx <= 0f) sx = 1f;
            if (sy <= 0f) sy = 1f;
            if (sz <= 0f) sz = 1f;

            foreach (var voxel in limb.Voxels)
            {
                // VXL Z is vertical. Map X -> Unity X, Z -> Unity Y, Y -> Unity Z.
                Vector3 center = new Vector3(
                    minX + (voxel.X + 0.5f) * sx,
                    minZ + (voxel.Z + 0.5f) * sz,
                    minY + (voxel.Y + 0.5f) * sy
                );

                Vector3 half = new Vector3(sx, sz, sy) * 0.5f;
                Color32 color = palette.Colors[voxel.Color];

                if (!limb.HasVoxel(voxel.X + 1, voxel.Y, voxel.Z))
                    AddFace(vertices, triangles, colors, center, half, 0, true, color);
                if (!limb.HasVoxel(voxel.X - 1, voxel.Y, voxel.Z))
                    AddFace(vertices, triangles, colors, center, half, 0, false, color);
                if (!limb.HasVoxel(voxel.X, voxel.Y, voxel.Z + 1))
                    AddFace(vertices, triangles, colors, center, half, 1, true, color);
                if (!limb.HasVoxel(voxel.X, voxel.Y, voxel.Z - 1))
                    AddFace(vertices, triangles, colors, center, half, 1, false, color);
                if (!limb.HasVoxel(voxel.X, voxel.Y + 1, voxel.Z))
                    AddFace(vertices, triangles, colors, center, half, 2, true, color);
                if (!limb.HasVoxel(voxel.X, voxel.Y - 1, voxel.Z))
                    AddFace(vertices, triangles, colors, center, half, 2, false, color);
            }

            var mesh = new Mesh();
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddFace(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color32> colors,
            Vector3 c,
            Vector3 h,
            int axis,
            bool positive,
            Color32 color
        )
        {
            Vector3[] v = new Vector3[4];

            if (axis == 0)
            {
                float x = c.x + (positive ? h.x : -h.x);
                v[0] = new Vector3(x, c.y - h.y, c.z - h.z);
                v[1] = new Vector3(x, c.y + h.y, c.z - h.z);
                v[2] = new Vector3(x, c.y + h.y, c.z + h.z);
                v[3] = new Vector3(x, c.y - h.y, c.z + h.z);
            }
            else if (axis == 1)
            {
                float y = c.y + (positive ? h.y : -h.y);
                v[0] = new Vector3(c.x - h.x, y, c.z - h.z);
                v[1] = new Vector3(c.x - h.x, y, c.z + h.z);
                v[2] = new Vector3(c.x + h.x, y, c.z + h.z);
                v[3] = new Vector3(c.x + h.x, y, c.z - h.z);
            }
            else
            {
                float z = c.z + (positive ? h.z : -h.z);
                v[0] = new Vector3(c.x - h.x, c.y - h.y, z);
                v[1] = new Vector3(c.x + h.x, c.y - h.y, z);
                v[2] = new Vector3(c.x + h.x, c.y + h.y, z);
                v[3] = new Vector3(c.x - h.x, c.y + h.y, z);
            }

            int start = vertices.Count;

            if (positive)
            {
                vertices.AddRange(v);
            }
            else
            {
                vertices.Add(v[3]);
                vertices.Add(v[2]);
                vertices.Add(v[1]);
                vertices.Add(v[0]);
            }

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void NormalizeVisualScale(GameObject visual, float targetSize)
        {
            var filters = visual.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0)
                return;

            Bounds bounds = filters[0].sharedMesh.bounds;
            for (int i = 1; i < filters.Length; i++)
                bounds.Encapsulate(filters[i].sharedMesh.bounds);

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest <= 0.0001f)
                return;

            float scale = targetSize / largest;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = -bounds.center * scale;
        }

        private static Material GetOrCreateVertexColorMaterial()
        {
            const string path = "Assets/Generated/RA2/Materials/RA2VertexColor.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("RA2/VertexColor");
            if (shader == null)
                throw new InvalidOperationException(
                    "Shader RA2/VertexColor was not found. Wait for Unity to finish compiling shaders."
                );

            material = new Material(shader) { name = "RA2VertexColor" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Limb";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value.Trim();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
