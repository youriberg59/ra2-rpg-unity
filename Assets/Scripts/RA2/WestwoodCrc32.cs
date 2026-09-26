using System;
using System.Text;

namespace RA2RPG.RA2
{
    public static class WestwoodCrc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data, uint initial = 0xFFFFFFFFu)
        {
            uint crc = initial;
            for (int i = 0; i < data.Length; i++)
                crc = (crc >> 8) ^ Table[(crc & 0xFF) ^ data[i]];
            return crc ^ initial;
        }

        public static uint HashFilename(string filename)
        {
            string name = filename.ToUpperInvariant();
            int originalLength = name.Length;
            int block = originalLength >> 2;

            if ((originalLength & 3) != 0)
            {
                int remainder = originalLength - (block << 2);
                name += (char)remainder;

                int paddingCount = 3 - (originalLength & 3);
                int sourceIndex = block << 2;
                char pad = name[sourceIndex < name.Length ? sourceIndex : 0];

                for (int i = 0; i < paddingCount; i++)
                    name += pad;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(name);
            return Compute(bytes);
        }

        private static uint[] BuildTable()
        {
            const uint polynomial = 0xEDB88320u;
            var table = new uint[256];

            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? polynomial ^ (value >> 1) : value >> 1;
                table[i] = value;
            }

            return table;
        }
    }
}
