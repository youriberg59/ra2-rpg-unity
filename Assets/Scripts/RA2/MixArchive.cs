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

        private readonly Stream stream;
        private readonly BinaryReader reader;
        private readonly Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
        private long dataStart;

        public string FilePath { get; }
        public string DisplayName { get; }
        public bool IsEncrypted { get; private set; }
        public bool HasChecksum { get; private set; }
        public int EntryCount => entries.Count;
        public IEnumerable<Entry> Entries => entries.Values;

        public MixArchive(string filePath)
        {
            FilePath = filePath;
            DisplayName = Path.GetFileName(filePath);
            stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(stream);
            ParseHeader();
        }

        public MixArchive(byte[] data, string displayName = "<memory.mix>")
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            FilePath = displayName;
            DisplayName = displayName;
            stream = new MemoryStream(data, writable: false);
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
                {
                    ParseEncryptedHeader();
                    return;
                }

                ParseTdHeader(stream.Position);
            }
            else
            {
                // Classic TD/RA style MIX: header begins at byte zero.
                ParseTdHeader(0);
            }
        }

        private void ParseEncryptedHeader()
        {
            const int keyBlockLength = 80;
            const int encryptedHeaderStart = 84; // flags (4) + RSA key block (80)

            stream.Position = 4;
            byte[] encryptedKey = reader.ReadBytes(keyBlockLength);
            if (encryptedKey.Length != keyBlockLength)
                throw new EndOfStreamException("Encrypted MIX key block is truncated.");

            byte[] blowfishKey = WestwoodMixCrypto.DeriveBlowfishKey(encryptedKey);

            // First encrypted block contains the 6-byte TD header plus the
            // beginning of the first entry, enough to recover the entry count.
            stream.Position = encryptedHeaderStart;
            byte[] firstEncryptedBlock = reader.ReadBytes(8);
            if (firstEncryptedBlock.Length != 8)
                throw new EndOfStreamException("Encrypted MIX header is truncated.");

            byte[] firstPlainBlock = WestwoodMixCrypto.BlowfishDecrypt(
                firstEncryptedBlock,
                blowfishKey
            );

            ushort count = BitConverter.ToUInt16(firstPlainBlock, 0);
            int plainHeaderLength = 6 + count * 12;
            int encryptedHeaderLength = Align8(plainHeaderLength);

            if (encryptedHeaderStart + encryptedHeaderLength > stream.Length)
                throw new InvalidDataException(
                    $"Encrypted MIX index is invalid: {count} entries exceed file length."
                );

            stream.Position = encryptedHeaderStart;
            byte[] encryptedHeader = reader.ReadBytes(encryptedHeaderLength);
            byte[] plainHeader = WestwoodMixCrypto.BlowfishDecrypt(
                encryptedHeader,
                blowfishKey
            );

            using var ms = new MemoryStream(plainHeader, writable: false);
            using var br = new BinaryReader(ms);

            ushort parsedCount = br.ReadUInt16();
            br.ReadUInt32(); // declared data size

            if (parsedCount != count)
                throw new InvalidDataException("Encrypted MIX header count mismatch.");

            entries.Clear();

            for (int i = 0; i < parsedCount; i++)
            {
                uint hash = br.ReadUInt32();
                uint offset = br.ReadUInt32();
                uint length = br.ReadUInt32();
                entries[hash] = new Entry(hash, offset, length);
            }

            dataStart = encryptedHeaderStart + encryptedHeaderLength;
        }

        private static int Align8(int value)
        {
            return (value + 7) & ~7;
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
                throw new FileNotFoundException($"'{filename}' was not found in {DisplayName}.");

            return ReadEntry(entry, filename);
        }

        public byte[] ReadEntry(Entry entry, string label = null)
        {
            long absoluteOffset = dataStart + entry.Offset;
            long end = absoluteOffset + entry.Length;

            if (absoluteOffset < 0 || end > stream.Length)
                throw new InvalidDataException(
                    $"Entry '{label ?? entry.Hash.ToString("X8")}' points outside the MIX archive."
                );

            stream.Position = absoluteOffset;
            byte[] data = reader.ReadBytes(checked((int)entry.Length));

            if (data.Length != entry.Length)
                throw new EndOfStreamException(
                    $"Could not read the complete MIX entry '{label ?? entry.Hash.ToString("X8")}'."
                );

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
