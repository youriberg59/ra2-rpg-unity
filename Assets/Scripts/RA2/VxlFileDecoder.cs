using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RA2RPG.RA2
{
    public sealed class VxlFileDecoder
    {
        public sealed class Voxel
        {
            public byte X;
            public byte Y;
            public byte Z;
            public byte Color;
            public byte Normal;
        }

        public sealed class Limb
        {
            public string Name;
            public float Scale;
            public float[] Bounds = new float[6];
            public byte SizeX;
            public byte SizeY;
            public byte SizeZ;
            public byte NormalType;
            public readonly List<Voxel> Voxels = new List<Voxel>();

            private HashSet<int> occupancy;

            public bool HasVoxel(int x, int y, int z)
            {
                occupancy ??= BuildOccupancy();
                return occupancy.Contains(Pack(x, y, z));
            }

            private HashSet<int> BuildOccupancy()
            {
                var set = new HashSet<int>();
                foreach (var v in Voxels)
                    set.Add(Pack(v.X, v.Y, v.Z));
                return set;
            }

            private static int Pack(int x, int y, int z)
            {
                return (x & 0xFF) | ((y & 0xFF) << 8) | ((z & 0xFF) << 16);
            }
        }

        public IReadOnlyList<Limb> Limbs => limbs;
        private readonly List<Limb> limbs = new List<Limb>();

        public static VxlFileDecoder Decode(byte[] data)
        {
            if (data == null || data.Length < 802)
                throw new InvalidDataException("VXL data is missing or too small.");

            var result = new VxlFileDecoder();

            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);

            string signature = Encoding.ASCII.GetString(reader.ReadBytes(16)).TrimEnd('\0');
            if (!signature.StartsWith("Voxel Animation", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid VXL header.");

            reader.ReadUInt32();
            uint limbCount = reader.ReadUInt32();
            reader.ReadUInt32();
            uint bodySize = reader.ReadUInt32();

            if (limbCount == 0 || limbCount > 1024)
                throw new InvalidDataException($"Invalid VXL limb count: {limbCount}.");

            stream.Seek(770, SeekOrigin.Current);

            var names = new string[limbCount];
            for (int i = 0; i < limbCount; i++)
            {
                names[i] = Encoding.ASCII.GetString(reader.ReadBytes(16)).TrimEnd('\0');
                stream.Seek(12, SeekOrigin.Current);
            }

            long dataBase = 802L + 28L * limbCount;
            long footerStart = dataBase + bodySize;

            if (footerStart < 0 || footerStart >= stream.Length)
                throw new InvalidDataException("Invalid VXL footer offset.");

            stream.Position = footerStart;

            var dataOffsets = new uint[limbCount];

            for (int i = 0; i < limbCount; i++)
            {
                dataOffsets[i] = reader.ReadUInt32();
                stream.Seek(8, SeekOrigin.Current);

                var limb = new Limb
                {
                    Name = names[i],
                    Scale = reader.ReadSingle()
                };

                stream.Seek(48, SeekOrigin.Current);

                for (int j = 0; j < 6; j++)
                    limb.Bounds[j] = reader.ReadSingle();

                limb.SizeX = reader.ReadByte();
                limb.SizeY = reader.ReadByte();
                limb.SizeZ = reader.ReadByte();
                limb.NormalType = reader.ReadByte();

                result.limbs.Add(limb);
            }

            for (int i = 0; i < limbCount; i++)
            {
                long offset = dataBase + dataOffsets[i];
                if (offset < 0 || offset >= stream.Length)
                    throw new InvalidDataException($"Invalid VXL limb data offset for limb {i}.");

                stream.Position = offset;
                ReadVoxelData(reader, result.limbs[i]);
            }

            return result;
        }

        private static void ReadVoxelData(BinaryReader reader, Limb limb)
        {
            int baseSize = limb.SizeX * limb.SizeY;
            if (baseSize <= 0)
                return;

            var columnStarts = new int[baseSize];

            for (int i = 0; i < baseSize; i++)
                columnStarts[i] = reader.ReadInt32();

            // Column end offsets; useful for validation but not needed for decoding.
            reader.BaseStream.Seek(4L * baseSize, SeekOrigin.Current);
            long dataStart = reader.BaseStream.Position;

            for (int column = 0; column < baseSize; column++)
            {
                int relative = columnStarts[column];
                if (relative < 0)
                    continue;

                long columnPos = dataStart + relative;
                if (columnPos < 0 || columnPos >= reader.BaseStream.Length)
                    continue;

                reader.BaseStream.Position = columnPos;

                int x = column % limb.SizeX;
                int y = column / limb.SizeX;
                int z = 0;
                int safety = 0;

                while (z < limb.SizeZ && safety++ < 1024)
                {
                    if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                        break;

                    z += reader.ReadByte();
                    int count = reader.ReadByte();

                    for (int j = 0; j < count && z < limb.SizeZ; j++)
                    {
                        if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                            break;

                        limb.Voxels.Add(new Voxel
                        {
                            X = (byte)x,
                            Y = (byte)y,
                            Z = (byte)z,
                            Color = reader.ReadByte(),
                            Normal = reader.ReadByte()
                        });

                        z++;
                    }

                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        break;

                    // Westwood stores the run length again after the voxel payload.
                    reader.ReadByte();
                }
            }
        }
    }
}
