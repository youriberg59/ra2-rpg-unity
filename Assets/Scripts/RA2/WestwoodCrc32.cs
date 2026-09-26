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
            byte[] upper = Encoding.ASCII.GetBytes(filename.ToUpperInvariant());
            int length = upper.Length;
            int residue = length & 3;

            if (residue == 0)
                return Compute(upper);

            int paddingCount = 4 - residue;
            int roundedPosition = length - residue;
            byte fill = upper[roundedPosition];

            byte[] padded = new byte[length + paddingCount];
            Buffer.BlockCopy(upper, 0, padded, 0, length);

            // Westwood RA2 padding:
            // first pad byte = residue length,
            // remaining pad bytes = byte at last 4-byte-aligned position.
            padded[length] = (byte)residue;

            for (int i = 1; i < paddingCount; i++)
                padded[length + i] = fill;

            return Compute(padded);
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
