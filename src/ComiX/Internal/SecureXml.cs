using System.Xml;
using System.Xml.Linq;

namespace ComiX.Internal;

/// <summary>
/// Loads XML metadata documents with entity resolution and DTD processing disabled, as archive
/// content is untrusted input. Shared by every XML metadata reader so the hardened settings are
/// defined once.
/// </summary>
internal static class SecureXml
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        CloseInput = false,
    };

    /// <summary>
    /// Parses <paramref name="stream"/> and returns the root element, verifying its local name.
    /// </summary>
    /// <param name="stream">The document content.</param>
    /// <param name="documentName">The document name used in exception messages.</param>
    /// <param name="expectedRoot">The expected root element local name, compared case-insensitively.</param>
    /// <exception cref="ComicMetadataException">
    /// The content is not well-formed XML, is empty, or has a different root element.
    /// </exception>
    public static XElement LoadRoot(Stream stream, string documentName, string expectedRoot)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(stream, Settings);
            document = XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new ComicMetadataException($"{documentName} is not well-formed XML.", ex);
        }

        var root = document.Root
            ?? throw new ComicMetadataException($"{documentName} is empty.");

        if (!root.Name.LocalName.Equals(expectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ComicMetadataException(
                $"Expected a {expectedRoot} root element but found '{root.Name.LocalName}'.");
        }

        return root;
    }
}
