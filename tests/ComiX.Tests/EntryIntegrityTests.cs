using ComiX.Archives;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// Verification of content read from an archive against what the container declares for the entry.
/// </summary>
public sealed class EntryIntegrityTests
{
    private static readonly byte[] Content = [0x41, 0x42, 0x43];

    // CRC-32 of "ABC".
    private const long ContentCrc = 0xA3830348;

    [Fact]
    public void Accepts_content_matching_the_recorded_checksum()
    {
        var entry = Entry(size: Content.Length, crc: ContentCrc);

        Assert.True(EntryIntegrity.Matches(entry, Content));
    }

    [Fact]
    public void Rejects_content_of_the_right_length_with_the_wrong_checksum()
    {
        // The failure mode the checksum exists for: a solid archive read against the wrong
        // decompression state returns the declared number of bytes from the wrong data.
        var entry = Entry(size: Content.Length, crc: ContentCrc);

        Assert.False(EntryIntegrity.Matches(entry, [0x58, 0x59, 0x5A]));
    }

    [Fact]
    public void Falls_back_to_the_declared_length_when_no_checksum_is_recorded()
    {
        var entry = Entry(size: Content.Length, crc: 0);

        Assert.True(EntryIntegrity.Matches(entry, Content));
        Assert.False(EntryIntegrity.Matches(entry, [0x41]));
    }

    [Fact]
    public void Rejects_content_for_an_entry_declared_empty()
    {
        var entry = Entry(size: 0, crc: 0);

        Assert.True(EntryIntegrity.Matches(entry, []));
        Assert.False(EntryIntegrity.Matches(entry, Content));
    }

    [Fact]
    public void Accepts_any_content_when_neither_checksum_nor_length_is_known()
    {
        var entry = Entry(size: -1, crc: 0);

        Assert.True(EntryIntegrity.Matches(entry, Content));
    }

    private static ComicArchiveEntry Entry(long size, long crc) =>
        new("1.jpg", size, IsDirectory: false, IsEncrypted: false, LastModified: null) { Crc = crc };
}
