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
                    // HVA stores a 3x4 affine matrix row by row.
                    float m00 = reader.ReadSingle();
                    float m01 = reader.ReadSingle();
                    float m02 = reader.ReadSingle();
                    float m03 = reader.ReadSingle();

                    float m10 = reader.ReadSingle();
                    float m11 = reader.ReadSingle();
                    float m12 = reader.ReadSingle();
                    float m13 = reader.ReadSingle();

                    float m20 = reader.ReadSingle();
                    float m21 = reader.ReadSingle();
                    float m22 = reader.ReadSingle();
                    float m23 = reader.ReadSingle();

                    var ra2 = new Matrix4x4();
                    ra2.m00 = m00; ra2.m01 = m01; ra2.m02 = m02; ra2.m03 = m03;
                    ra2.m10 = m10; ra2.m11 = m11; ra2.m12 = m12; ra2.m13 = m13;
                    ra2.m20 = m20; ra2.m21 = m21; ra2.m22 = m22; ra2.m23 = m23;
                    ra2.m30 = 0f;  ra2.m31 = 0f;  ra2.m32 = 0f;  ra2.m33 = 1f;

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
