internal sealed class IceCipher
{
    private const int Rounds = 16;

    private static readonly int[][] SMod =
    [
        [333, 313, 505, 369],
        [379, 375, 319, 391],
        [361, 445, 451, 397],
        [397, 425, 395, 505],
    ];

    private static readonly int[][] SXor =
    [
        [0x83, 0x85, 0x9b, 0xcd],
        [0xcc, 0xa7, 0xad, 0x41],
        [0x4b, 0x2e, 0xd4, 0x33],
        [0xea, 0xcb, 0x2e, 0x04],
    ];

    private static readonly uint[] PBox =
    [
        0x00000001, 0x00000080, 0x00000400, 0x00002000,
        0x00080000, 0x00200000, 0x01000000, 0x40000000,
        0x00000008, 0x00000020, 0x00000100, 0x00004000,
        0x00010000, 0x00800000, 0x04000000, 0x20000000,
        0x00000004, 0x00000010, 0x00000200, 0x00008000,
        0x00020000, 0x00400000, 0x08000000, 0x10000000,
        0x00000002, 0x00000040, 0x00000800, 0x00001000,
        0x00040000, 0x00100000, 0x02000000, 0x80000000,
    ];

    private static readonly int[] KeyRotation =
    [
        0, 1, 2, 3, 2, 1, 3, 0,
        1, 3, 2, 0, 3, 1, 0, 2,
    ];

    private static readonly uint[][] SBoxes = BuildSBoxes();

    private readonly uint[][] keySchedule;

    internal IceCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length != 8)
            throw new ArgumentException("ICE level 1 requires 8 key bytes.", nameof(key));

        keySchedule = new uint[Rounds][];
        for (var i = 0; i < Rounds; i++)
            keySchedule[i] = new uint[3];

        Span<ushort> keyBlock = stackalloc ushort[4];
        FillKeyBlock(key, keyBlock);
        BuildSchedule(keyBlock, 0, KeyRotation.AsSpan(0, 8));
        BuildSchedule(keyBlock, Rounds - 8, KeyRotation.AsSpan(8, 8));
    }

    internal byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length == 0 || plaintext.Length % 8 != 0)
            throw new ArgumentException("ICE plaintext must contain complete 8-byte blocks.", nameof(plaintext));

        var ciphertext = new byte[plaintext.Length];
        for (var offset = 0; offset < plaintext.Length; offset += 8)
            EncryptBlock(plaintext.Slice(offset, 8), ciphertext.AsSpan(offset, 8));
        return ciphertext;
    }

    private void EncryptBlock(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        var left = ReadBigEndianUInt32(plaintext);
        var right = ReadBigEndianUInt32(plaintext[4..]);

        for (var i = 0; i < Rounds; i += 2)
        {
            left ^= RoundFunction(right, keySchedule[i]);
            right ^= RoundFunction(left, keySchedule[i + 1]);
        }

        WriteBigEndianUInt32(ciphertext, right);
        WriteBigEndianUInt32(ciphertext[4..], left);
    }

    private void BuildSchedule(Span<ushort> keyBlock, int scheduleOffset, ReadOnlySpan<int> rotations)
    {
        for (var i = 0; i < 8; i++)
        {
            var rotation = rotations[i];
            var subkey = keySchedule[scheduleOffset + i];
            Array.Clear(subkey);

            for (var j = 0; j < 15; j++)
            {
                for (var k = 0; k < 4; k++)
                {
                    var keyIndex = (rotation + k) & 3;
                    var bit = keyBlock[keyIndex] & 1;
                    subkey[j % 3] = (subkey[j % 3] << 1) | (uint)bit;
                    keyBlock[keyIndex] = (ushort)((keyBlock[keyIndex] >> 1) | ((bit ^ 1) << 15));
                }
            }
        }
    }

    private static void FillKeyBlock(ReadOnlySpan<byte> key, Span<ushort> keyBlock)
    {
        for (var i = 0; i < 4; i++)
            keyBlock[3 - i] = (ushort)((key[i * 2] << 8) | key[i * 2 + 1]);
    }

    private static uint RoundFunction(uint value, uint[] subkey)
    {
        var left = ((value >> 16) & 0x3ff) | (((value >> 14) | (value << 18)) & 0xffc00);
        var right = (value & 0x3ff) | ((value << 2) & 0xffc00);
        var saltedLeft = subkey[2] & (left ^ right);
        var saltedRight = saltedLeft ^ right;
        saltedLeft ^= left;
        saltedLeft ^= subkey[0];
        saltedRight ^= subkey[1];

        return SBoxes[0][saltedLeft >> 10]
            | SBoxes[1][saltedLeft & 0x3ff]
            | SBoxes[2][saltedRight >> 10]
            | SBoxes[3][saltedRight & 0x3ff];
    }

    private static uint[][] BuildSBoxes()
    {
        var boxes = new uint[4][];
        for (var i = 0; i < boxes.Length; i++)
            boxes[i] = new uint[1024];

        for (var i = 0; i < 1024; i++)
        {
            var column = (i >> 1) & 0xff;
            var row = (i & 1) | ((i & 0x200) >> 8);
            boxes[0][i] = Permute32(GfExp7((uint)(column ^ SXor[0][row]), (uint)SMod[0][row]) << 24);
            boxes[1][i] = Permute32(GfExp7((uint)(column ^ SXor[1][row]), (uint)SMod[1][row]) << 16);
            boxes[2][i] = Permute32(GfExp7((uint)(column ^ SXor[2][row]), (uint)SMod[2][row]) << 8);
            boxes[3][i] = Permute32(GfExp7((uint)(column ^ SXor[3][row]), (uint)SMod[3][row]));
        }

        return boxes;
    }

    private static uint GfExp7(uint value, uint modulus)
    {
        if (value == 0)
            return 0;

        var result = GfMultiply(value, value, modulus);
        result = GfMultiply(value, result, modulus);
        result = GfMultiply(result, result, modulus);
        return GfMultiply(value, result, modulus);
    }

    private static uint GfMultiply(uint left, uint right, uint modulus)
    {
        uint result = 0;
        while (right != 0)
        {
            if ((right & 1) != 0)
                result ^= left;
            left <<= 1;
            right >>= 1;
            if (left >= 256)
                left ^= modulus;
        }
        return result;
    }

    private static uint Permute32(uint value)
    {
        uint result = 0;
        var pboxIndex = 0;
        while (value != 0)
        {
            if ((value & 1) != 0)
                result |= PBox[pboxIndex];
            pboxIndex++;
            value >>= 1;
        }
        return result;
    }

    private static uint ReadBigEndianUInt32(ReadOnlySpan<byte> value) =>
        ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];

    private static void WriteBigEndianUInt32(Span<byte> destination, uint value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }
}
