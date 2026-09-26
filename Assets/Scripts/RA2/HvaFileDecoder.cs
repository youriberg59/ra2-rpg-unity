using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RA2RPG.RA2
{
    public sealed class HvaFileDecoder
    {
        public sealed class LimbTrack
        {
            public string Name;
            public readonly List<Matrix4x4> Frames = new List<Matrix4x4>();
        }

        public int FrameCount { get; private set; }
        public int LimbCount { get; private set; }
        public IReadOnlyList<LimbTrack> Limbs => limbs;

        private readonly List<LimbTrack> limbs = new List<LimbTrack>();

        public static HvaFileDecoder Decode(byte[] data)
        {
            if (data == null || data.Length < 24)
                throw new InvalidDataException("HVA data is missing or too small.");

            var result = new HvaFileDecoder();

            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);

            // 16-byte file/header name.
            reader.ReadBytes(16);

            uint frameCount = reader.ReadUInt32();
            uint limbCount = reader.ReadUInt32();

            if (frameCount == 0 || frameCount > 100000)
                throw new InvalidDataException($"Invalid HVA frame count: {frameCount}.");

            if (limbCount == 0 || limbCount > 1024)
                throw new InvalidDataException($"Invalid HVA limb count: {limbCount}.");

            result.FrameCount = checked((int)frameCount);
            result.LimbCount = checked((int)limbCount);

            for (int i = 0; i < result.LimbCount; i++)
            {
                string name = Encoding.ASCII
                    .GetString(reader.ReadBytes(16))
                    .TrimEnd('\0', ' ');

                result.limbs.Add(new LimbTrack { Name = name });
            }

            long required = 24L + 16L * result.LimbCount +
                48L * result.FrameCount * result.LimbCount;

            if (required > data.Length)
                throw new InvalidDataException(
                    $"HVA transform data is truncated. Expected at least {required} bytes, got {data.Length}."
                );

            for (int frame = 0; frame < result.FrameCount; frame++)
            {
                for (int limb = 0; limb < result.LimbCount; limb++)
                {
                    // Westwood HVA stores 12 affine values in row-major order,
                    // while the consumers of the format treat them as a transposed
                    // column-major 4x4 matrix. Reconstruct that matrix explicitly.
                    float[] values = new float[12];
                    for (int k = 0; k < 12; k++)
                        values[k] = reader.ReadSingle();

                    // Equivalent to the canonical HVA transpose mapping:
                    // source 0..11 -> destination 0,4,8,12,1,5,9,13,2,6,10,14.
                    var ra2 = Matrix4x4.identity;
                    ra2[0]  = values[0];
                    ra2[4]  = values[1];
                    ra2[8]  = values[2];
                    ra2[12] = values[3];

                    ra2[1]  = values[4];
                    ra2[5]  = values[5];
                    ra2[9]  = values[6];
                    ra2[13] = values[7];

                    ra2[2]  = values[8];
                    ra2[6]  = values[9];
                    ra2[10] = values[10];
                    ra2[14] = values[11];

                    ra2[3] = 0f;
                    ra2[7] = 0f;
                    ra2[11] = 0f;
                    ra2[15] = 1f;

                    result.limbs[limb].Frames.Add(ConvertRa2ToUnity(ra2));
                }
            }

            return result;
        }

        public bool TryGetFrame(string limbName, int frame, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;

            if (frame < 0 || frame >= FrameCount)
                return false;

            foreach (var limb in limbs)
            {
                if (!string.Equals(limb.Name, limbName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (frame >= limb.Frames.Count)
                    return false;

                matrix = limb.Frames[frame];
                return true;
            }

            return false;
        }

        private static Matrix4x4 ConvertRa2ToUnity(Matrix4x4 ra2)
        {
            // Our VXL mesh conversion uses RA2 (X,Y,Z) -> Unity (X,Z,Y).
            // Apply the same basis change to HVA transforms: U = P * R * P^-1.
            var p = Matrix4x4.identity;
            p.m11 = 0f;
            p.m12 = 1f;
            p.m21 = 1f;
            p.m22 = 0f;

            return p * ra2 * p;
        }
    }
}
