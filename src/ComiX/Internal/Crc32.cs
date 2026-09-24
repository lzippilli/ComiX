namespace ComiX.Internal;

/// <summary>
/// CRC-32 as defined by ISO 3309, the checksum ZIP, RAR and 7-Zip record for entry content.
/// </summary>
/// <remarks>
/// Implemented here rather than taken from <c>System.IO.Hashing</c> so that the library keeps a
/// single runtime dependency. The table is built once and the implementation processes one byte at a
/// time, which is sufficient: the checksum is computed only for archives that need the content of a
/// read verified.
/// </remarks>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = BuildTable();

    /// <summary>Computes the checksum of <paramref name="data"/>.</summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (var index = 0u; index < table.Length; index++)
        {
            var value = index;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
