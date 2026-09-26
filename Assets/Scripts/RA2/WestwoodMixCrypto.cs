using System;
using System.IO;
using System.Numerics;
using BlowfishManaged;
using BlowfishManaged.KeyGeneration;

namespace RA2RPG.RA2
{
    internal static class WestwoodMixCrypto
    {
        private const string ModulusDerBase64 =
            "AihRvNoIbTn85FZRYNZRcT+i6KpU+maCsEqr3Q5q+LDB5tH7Tz2qQ38V";

        public static byte[] DeriveBlowfishKey(byte[] encrypted80)
        {
            if (encrypted80 == null || encrypted80.Length != 80)
                throw new ArgumentException("Westwood MIX key block must be exactly 80 bytes.");

            byte[] modulusDer = Convert.FromBase64String(ModulusDerBase64);
            byte[] modulusBigEndian = ReadDerInteger(modulusDer);
            BigInteger modulus = FromBigEndianUnsigned(modulusBigEndian);
            BigInteger exponent = new BigInteger(65537);

            // Westwood's key decoder treats the 80-byte predata as two
            // 40-byte RSA blocks. Each block yields 39 bytes of plaintext
            // (pubkey bit length minus one byte), for 78 intermediate bytes.
            // The Blowfish key is the first 56 bytes of that stream.
            const int cipherBlockSize = 40;
            const int plainBlockSize = 39;

            var decoded = new byte[plainBlockSize * 2];

            for (int part = 0; part < 2; part++)
            {
                byte[] cipherLittleEndian = new byte[cipherBlockSize];
                Buffer.BlockCopy(
                    encrypted80,
                    part * cipherBlockSize,
                    cipherLittleEndian,
                    0,
                    cipherBlockSize
                );

                BigInteger cipher = FromLittleEndianUnsigned(cipherLittleEndian);
                BigInteger plain = BigInteger.ModPow(cipher, exponent, modulus);
                byte[] plainLittleEndian = ToLittleEndianUnsigned(plain);

                if (plainLittleEndian.Length > plainBlockSize)
                    throw new InvalidDataException(
                        $"Unexpected Westwood RSA plaintext size {plainLittleEndian.Length}; expected <= {plainBlockSize}."
                    );

                // The original implementation copies a fixed 39-byte little-endian
                // bignum buffer, zero-padded when necessary.
                Buffer.BlockCopy(
                    plainLittleEndian,
                    0,
                    decoded,
                    part * plainBlockSize,
                    plainLittleEndian.Length
                );
            }

            var key = new byte[56];
            Buffer.BlockCopy(decoded, 0, key, 0, key.Length);
            return key;
        }

        public static byte[] BlowfishDecrypt(byte[] encrypted, byte[] key)
        {
            if (encrypted == null)
                throw new ArgumentNullException(nameof(encrypted));
            if (key == null || key.Length == 0)
                throw new ArgumentException("Blowfish key is missing.");
            if ((encrypted.Length & 7) != 0)
                throw new ArgumentException("Blowfish input must be a multiple of 8 bytes.");

            var context = new BlowfishContext(key);
            var result = new byte[encrypted.Length];

            for (int offset = 0; offset < encrypted.Length; offset += 8)
            {
                ulong block = ByteOperations.PackBytesIntoUInt64(encrypted, offset);
                ulong decrypted = BlowfishEngine.Decrypt(block, context);
                byte[] bytes = ByteOperations.UnpackUInt64IntoBytes(decrypted);
                Buffer.BlockCopy(bytes, 0, result, offset, 8);
            }

            return result;
        }

        private static byte[] ReadDerInteger(byte[] der)
        {
            int index = 0;

            if (der.Length < 2 || der[index++] != 0x02)
                throw new InvalidDataException("Invalid Westwood RSA modulus encoding.");

            int length = der[index++];
            if ((length & 0x80) != 0)
            {
                int lengthBytes = length & 0x7F;
                length = 0;
                for (int i = 0; i < lengthBytes; i++)
                    length = (length << 8) | der[index++];
            }

            var integer = new byte[length];
            Buffer.BlockCopy(der, index, integer, 0, length);
            return integer;
        }

        private static BigInteger FromBigEndianUnsigned(byte[] bigEndian)
        {
            var little = new byte[bigEndian.Length + 1];
            for (int i = 0; i < bigEndian.Length; i++)
                little[i] = bigEndian[bigEndian.Length - 1 - i];
            little[little.Length - 1] = 0;
            return new BigInteger(little);
        }

        private static BigInteger FromLittleEndianUnsigned(byte[] littleEndian)
        {
            var signedSafe = new byte[littleEndian.Length + 1];
            Buffer.BlockCopy(littleEndian, 0, signedSafe, 0, littleEndian.Length);
            signedSafe[signedSafe.Length - 1] = 0;
            return new BigInteger(signedSafe);
        }

        private static byte[] ToLittleEndianUnsigned(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();

            if (bytes.Length > 1 && bytes[bytes.Length - 1] == 0)
            {
                Array.Resize(ref bytes, bytes.Length - 1);
            }

            return bytes;
        }
    }
}
