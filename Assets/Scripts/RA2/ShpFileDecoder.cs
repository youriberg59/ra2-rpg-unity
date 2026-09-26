using System;
using System.Collections.Generic;
using System.IO;

namespace RA2RPG.RA2
{
    public sealed class ShpFileDecoder
    {
        public sealed class Frame
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public byte Compression;
            public byte[] Pixels;
        }

        public int CanvasWidth { get; private set; }
        public int CanvasHeight { get; private set; }
        public IReadOnlyList<Frame> Frames => frames;

        private readonly List<Frame> frames = new List<Frame>();

        private struct Header
        {
            public short X;
            public short Y;
            public short Width;
            public short Height;
            public byte Compression;
            public int DataOffset;
        }

        public static ShpFileDecoder Decode(byte[] data)
        {
            if (data == null || data.Length < 8)
                throw new InvalidDataException("SHP data is missing or too small.");

            var result = new ShpFileDecoder();

            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);

            short reserved = reader.ReadInt16();
            result.CanvasWidth = reader.ReadUInt16();
            result.CanvasHeight = reader.ReadUInt16();
            ushort count = reader.ReadUInt16();

            if (reserved != 0)
                throw new InvalidDataException(
                    $"Unsupported SHP variant: reserved/header field is {reserved}, expected 0."
                );

            if (count == 0 || count > 4096)
                throw new InvalidDataException($"Invalid SHP frame count: {count}.");

            const int descriptorSize = 24;
            long descriptorsEnd = 8L + count * descriptorSize;
            if (descriptorsEnd > data.Length)
                throw new InvalidDataException("SHP frame descriptor table exceeds file size.");

            var headers = new Header[count];

            for (int i = 0; i < count; i++)
            {
                var h = new Header
                {
                    X = reader.ReadInt16(),
                    Y = reader.ReadInt16(),
                    Width = reader.ReadInt16(),
                    Height = reader.ReadInt16(),
                    Compression = reader.ReadByte()
                };

                reader.ReadBytes(3);
                reader.ReadInt32();
                reader.ReadInt32();
                h.DataOffset = reader.ReadInt32();

                headers[i] = h;
            }

            for (int i = 0; i < count; i++)
            {
                Header h = headers[i];

                if (h.Width < 0 || h.Height < 0)
                    throw new InvalidDataException($"Invalid SHP frame dimensions at frame {i}.");

                int pixelCount = h.Width * h.Height;
                int nextOffset = i + 1 < count ? headers[i + 1].DataOffset : data.Length;

                if (nextOffset < h.DataOffset || nextOffset > data.Length)
                    nextOffset = data.Length;

                if (h.DataOffset < 0 || h.DataOffset > data.Length)
                    throw new InvalidDataException($"Invalid SHP frame data offset at frame {i}.");

                int compressedLength = nextOffset - h.DataOffset;
                stream.Position = h.DataOffset;

                byte[] pixels = DecodeFrame(
                    reader,
                    h.Width,
                    h.Height,
                    h.Compression,
                    compressedLength
                );

                if (pixels.Length != pixelCount)
                {
                    var normalized = new byte[pixelCount];
                    Buffer.BlockCopy(pixels, 0, normalized, 0, Math.Min(pixels.Length, normalized.Length));
                    pixels = normalized;
                }

                result.frames.Add(new Frame
                {
                    X = h.X,
                    Y = h.Y,
                    Width = h.Width,
                    Height = h.Height,
                    Compression = h.Compression,
                    Pixels = pixels
                });
            }

            return result;
        }

        private static byte[] DecodeFrame(
            BinaryReader reader,
            int width,
            int height,
            byte compression,
            int compressedLength
        )
        {
            int size = width * height;
            if (size == 0)
                return Array.Empty<byte>();

            if (compression <= 1)
            {
                byte[] raw = reader.ReadBytes(Math.Min(size, Math.Max(0, compressedLength)));
                var output = new byte[size];
                Buffer.BlockCopy(raw, 0, output, 0, Math.Min(raw.Length, output.Length));
                return output;
            }

            if (compression == 2)
                return DecodeType2(reader, width, height);

            if (compression == 3)
            {
                byte[] source = reader.ReadBytes(Math.Max(0, compressedLength));
                return DecodeType3(source, width, height);
            }

            throw new InvalidDataException($"Unsupported SHP compression type: {compression}.");
        }

        private static byte[] DecodeType2(BinaryReader reader, int width, int height)
        {
            var output = new byte[width * height];
            int dest = 0;

            for (int y = 0; y < height; y++)
            {
                if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                    break;

                int lineLength = reader.ReadUInt16() - 2;
                if (lineLength < 0)
                    break;

                byte[] line = reader.ReadBytes(lineLength);
                int copy = Math.Min(line.Length, output.Length - dest);
                Buffer.BlockCopy(line, 0, output, dest, copy);

                dest += width;
                if (dest > output.Length)
                    dest = output.Length;
            }

            return output;
        }

        // Westwood SHP compression type 3: each scanline starts with a little-endian
        // byte count (including the 2-byte length itself). Nonzero bytes are literal
        // palette indices; zero byte followed by N means N transparent pixels.
        private static byte[] DecodeType3(byte[] source, int width, int height)
        {
            var output = new byte[width * height];
            int src = 0;

            for (int y = 0; y < height; y++)
            {
                if (src + 2 > source.Length)
                    break;

                int lineBytes = source[src] | (source[src + 1] << 8);
                src += 2;
                lineBytes -= 2;

                int end = Math.Min(source.Length, src + Math.Max(0, lineBytes));
                int x = 0;

                while (src < end && x < width)
                {
                    byte value = source[src++];

                    if (value != 0)
                    {
                        output[y * width + x] = value;
                        x++;
                    }
                    else
                    {
                        if (src >= end)
                            break;

                        int run = source[src++];
                        int max = Math.Min(run, width - x);
                        x += max;
                    }
                }

                src = end;
            }

            return output;
        }
    }
}
