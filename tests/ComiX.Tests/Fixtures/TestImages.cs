namespace ComiX.Tests.Fixtures;

/// <summary>
/// Minimal image payloads. ComiX never decodes images, so a correct header plus a marker byte is
/// enough to exercise everything the library actually does.
/// </summary>
internal static class TestImages
{
    public static byte[] Jpeg(byte marker) =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, marker, 0x00, 0xFF, 0xD9];

    public static byte[] Png(byte marker) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, marker];

    /// <summary>
    /// A payload large enough for a compressor to build a dictionary from, which the smaller ones
    /// are not. Required by the solid archive fixture: an entry of a few bytes leaves nothing in the
    /// shared window, and the behaviour under test does not occur.
    /// </summary>
    /// <param name="marker">Selects the byte sequence, so that entries are distinguishable.</param>
    public static byte[] LargeJpeg(byte marker)
    {
        var content = new byte[2048];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        content[3] = 0xE0;

        // A linear congruential sequence: reproducible across runtimes, and restricted to an
        // alphabet of 26 values so that the content compresses rather than being stored verbatim.
        var state = unchecked((marker * 2654435761u) + 1u);
        for (var index = 4; index < content.Length - 2; index++)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            content[index] = (byte)(0x61 + ((state >> 16) % 26));
        }

        content[^2] = 0xFF;
        content[^1] = 0xD9;

        return content;
    }

    public static byte[] NotAnImage() => "this file is not an image"u8.ToArray();
}
