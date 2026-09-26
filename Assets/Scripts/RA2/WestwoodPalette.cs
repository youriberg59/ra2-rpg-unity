using System;
using System.IO;
using UnityEngine;

namespace RA2RPG.RA2
{
    public sealed class WestwoodPalette
    {
        public const int ColorCount = 256;
        public const int ByteLength = ColorCount * 3;

        public Color32[] Colors { get; } = new Color32[ColorCount];

        public static WestwoodPalette FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < ByteLength)
                throw new InvalidDataException(
                    $"A Westwood PAL requires at least {ByteLength} bytes."
                );

            var palette = new WestwoodPalette();

            for (int i = 0; i < ColorCount; i++)
            {
                int p = i * 3;

                // Westwood palette channels are six-bit values (0..63).
                palette.Colors[i] = new Color32(
                    Expand6Bit(bytes[p]),
                    Expand6Bit(bytes[p + 1]),
                    Expand6Bit(bytes[p + 2]),
                    255
                );
            }

            return palette;
        }

        public Texture2D CreatePreviewTexture(int columns = 16, int cellSize = 16)
        {
            int rows = Mathf.CeilToInt(ColorCount / (float)columns);
            var texture = new Texture2D(
                columns * cellSize,
                rows * cellSize,
                TextureFormat.RGBA32,
                false
            )
            {
                name = "RA2 Palette Preview",
                filterMode = FilterMode.Point
            };

            var pixels = new Color32[texture.width * texture.height];

            for (int i = 0; i < ColorCount; i++)
            {
                int cx = i % columns;
                int cy = rows - 1 - (i / columns);

                for (int y = 0; y < cellSize; y++)
                for (int x = 0; x < cellSize; x++)
                    pixels[(cy * cellSize + y) * texture.width + cx * cellSize + x] = Colors[i];
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static byte Expand6Bit(byte value)
        {
            int sixBit = Math.Min(value, (byte)63);
            return (byte)Math.Min(255, sixBit * 4);
        }
    }
}
