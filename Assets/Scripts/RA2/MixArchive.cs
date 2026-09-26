using System;
using System.Collections.Generic;
using System.IO;

namespace RA2RPG.RA2
{
    [Flags]
    public enum MixFlags : uint
    {
        Checksum = 0x00010000,
        Encrypted = 0x00020000
    }

    public sealed class MixArchive : IDisposable
    {
        public readonly struct Entry
        {
            public readonly uint Hash;
            public readonly uint Offset;
            public readonly uint Length;

            public Entry(uint hash, uint offset, uint length)
            {
                Hash = hash;
                Offset = offset;
                Length = length;
            }
        }

        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
        private long dataStart;

        public string FilePath { get; }
        public bool IsEncrypted { get; private set; }
        public bool HasChecksum { get; private set; }
        public int EntryCount => entries.Count;

        public MixArchive(string filePath)
        {
            FilePath = filePath;
            stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(stream);
            ParseHeader();
        }

        private void ParseHeader()
        {
            if (stream.Length < 6)
                throw new InvalidDataException("File is too small to be a MIX archive.");

            stream.Position = 0;
            uint possibleFlags = reader.ReadUInt32();
            uint knownMask = (uint)(MixFlags.Checksum | MixFlags.Encrypted);

            bool looksLikeFlagHeader = (possibleFlags & ~knownMask) == 0;

            if (looksLikeFlagHeader)
            {
                IsEncrypted = (possibleFlags & (uint)MixFlags.Encrypted) != 0;
                HasChecksum = (possibleFlags & (uint)MixFlags.Checksum) != 0;

                if (IsEncrypted)
                    throw new NotSupportedException(
                        "Encrypted Westwood MIX header detected. Encrypted MIX support is the next importer milestone."
                    );

                ParseTdHeader(stream.Position);
            }
            else
            {
                // Classic TD/RA style MIX: header begins at byte zero.
                ParseTdHeader(0);
            }
        }

        private void ParseTdHeader(long headerOffset)
        {
            stream.Position = headerOffset;

            ushort count = reader.ReadUInt16();
            uint declaredDataSize = reader.ReadUInt32();

            long indexBytes = count * 12L;
            long candidateDataStart = stream.Position + indexBytes;

            if (candidateDataStart > stream.Length)
                throw new InvalidDataException(
                    $"Invalid MIX index: {count} entries exceed file length."
                );

            entries.Clear();

            for (int i = 0; i < count; i++)
            {
                uint hash = reader.ReadUInt32();
                uint offset = reader.ReadUInt32();
                uint length = reader.ReadUInt32();

                entries[hash] = new Entry(hash, offset, length);
            }

            dataStart = candidateDataStart;

            // Basic sanity check. Some archives have padding/checksum bytes, so don't
            // require exact equality with the physical file size.
            if (declaredDataSize > stream.Length)
                throw new InvalidDataException("MIX declared data size is larger than the archive.");
        }

        public bool Contains(string filename)
        {
            return entries.ContainsKey(WestwoodCrc32.HashFilename(filename));
        }

        public bool TryGetEntry(string filename, out Entry entry)
        {
            return entries.TryGetValue(WestwoodCrc32.HashFilename(filename), out entry);
        }

        public byte[] ReadFile(string filename)
        {
            if (!TryGetEntry(filename, out Entry entry))
                throw new FileNotFoundException($"'{filename}' was not found in {Path.GetFileName(FilePath)}.");

            long absoluteOffset = dataStart + entry.Offset;
            long end = absoluteOffset + entry.Length;

            if (absoluteOffset < 0 || end > stream.Length)
                throw new InvalidDataException(
                    $"Entry '{filename}' points outside the MIX archive."
                );

            stream.Position = absoluteOffset;
            byte[] data = reader.ReadBytes(checked((int)entry.Length));

            if (data.Length != entry.Length)
                throw new EndOfStreamException($"Could not read the complete MIX entry '{filename}'.");

            return data;
        }

        public void Extract(string filename, string destinationPath)
        {
            byte[] data = ReadFile(filename);
            string directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(destinationPath, data);
        }

        public void Dispose()
        {
            reader?.Dispose();
            stream?.Dispose();
        }
    }
}
