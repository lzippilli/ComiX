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

    public static byte[] NotAnImage() => "this file is not an image"u8.ToArray();
}
