# ComiX

A .NET library for reading, extracting and converting comic book archives.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/lzippilli/ComiX/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/vpre/ComiX.svg)](https://www.nuget.org/packages/ComiX)

ComiX exposes a unified object model for comic archives that is independent of the underlying
container format and of the metadata standard the archive was tagged with. It provides archive access,
metadata normalisation, extraction and format conversion for applications such as readers, cataloguing
tools and conversion utilities. Image rendering and decoding are outside its scope.

```csharp
await using var comic = await ComicBook.OpenAsync("example.cbz");

Console.WriteLine(comic.Metadata.Series);
Console.WriteLine(comic.Pages.Count);

await using var page = await comic.Pages[0].OpenAsync();
```

## Supported formats

| Container | Detection | Reading | Conversion to CBZ |
| :--- | :---: | :---: | :---: |
| CBZ (ZIP) | ✔ | ✔ | ✔ |
| CBR (RAR) | ✔ | ✔ | ✔ |
| CB7 (7-Zip) | ✔ | ✔ | ✔ |
| CBT (TAR) | ✔ | ✔ | ✔ |

| Metadata standard | Location | Reading | Writing | Per-page information |
| :--- | :--- | :---: | :---: | :---: |
| `ComicInfo.xml` | archive entry | ✔ | ✘ | ✔ |
| `MetronInfo.xml` | archive entry | ✔ | ✘ | ✘ |
| CoMet | `comet.xml` archive entry | ✔ | ✘ | ✘ |
| ComicBookInfo | archive comment (ZIP and RAR only) | ✔ | ✘ | ✘ |

Writing metadata into an existing archive is not supported yet. Conversion copies the original metadata
entries and the archive comment verbatim.

Supported page formats, identified by file extension: JPEG, PNG, GIF, WebP, BMP, TIFF and AVIF.

## Installation

```bash
dotnet add package ComiX
```

Target frameworks: `net8.0` and `net10.0`. The only runtime dependency is
[SharpCompress](https://github.com/adamhathcock/sharpcompress) (MIT).

## Usage

### Opening an archive

```csharp
await using var comic = await ComicBook.OpenAsync("example.cbz");
```

ComiX determines the container format from the file contents. The file extension is not used as the
authoritative source for container identification.

`ComicBook.OpenAsync` also accepts a `Stream`, which must be readable and seekable. The stream is not
disposed by the `ComicBook` unless `leaveOpen: false` is specified.

```csharp
await using var stream = File.OpenRead("example.cbz");
await using var comic = await ComicBook.OpenAsync(stream);
```

Opening an archive reads the container index and resolves metadata. Page content is accessed lazily
and is not extracted or decoded during this operation.

### Pages

`ComicBook.Pages` returns the archive's images in reading order. Ordering is numeric-aware, so
`2.jpg` precedes `10.jpg`; a file named `cover` at the archive root is ordered first.

```csharp
foreach (var page in comic.Pages)
{
    Console.WriteLine($"{page.Index}: {page.Name} ({page.ImageFormat}, {page.SizeInBytes} bytes)");
}
```

`ComicPage.OpenAsync` returns a seekable, read-only `Stream` over the page content. The stream is
owned by the caller, is independent of the `ComicBook` that produced it, and remains readable after
that instance has been disposed.

```csharp
await using var content = await comic.Pages[0].OpenAsync();
```

Page attributes derived from metadata — `DeclaredWidth`, `DeclaredHeight`, `Type`, `DeclaredSpread`
and `Bookmark` — are populated only when the source metadata declares them. ComiX does not decode
image content.

### Resources

Entries that are not images, including the metadata file itself, are exposed through
`ComicBook.Resources` with the same content access API as pages.

```csharp
var comicInfo = comic.Resources.FirstOrDefault(resource => resource.FileName == "ComicInfo.xml");
```

### Metadata

Metadata from any supported standard is normalised into a single `ComicMetadata` instance. Values
that ComiX does not map to a canonical property are retained in `ComicMetadata.Extensions` under their
original field name.

```csharp
var metadata = comic.Metadata;

Console.WriteLine(metadata.Title);
Console.WriteLine(metadata.Series);
Console.WriteLine(metadata.PublicationDate);
Console.WriteLine(metadata.Extensions["StoryArc"]);

foreach (var credit in metadata.Credits)
{
    Console.WriteLine($"{credit.Role}: {credit.Name}");
}
```

Credits are modelled as one entry per person and role. A person credited for two roles produces two
entries; group by `ComicCredit.Name` to obtain one entry per person.

`ComicCreditRole` normalises equivalent role names across standards: `Script`, `Story` and `Author`
all resolve to `ComicCreditRole.Writer`. Comparison is case-insensitive, and unrecognised roles are
preserved verbatim.

```csharp
var writers = metadata.Credits.Where(credit => credit.Role == ComicCreditRole.Writer);
```

### Metadata resolution

An archive may contain more than one metadata source. Sources are resolved in the following
precedence order: `ComicInfo.xml`, `MetronInfo.xml`, CoMet, then ComicBookInfo. The first source
that parses successfully supplies the canonical values; the remaining sources are reported but not
merged.

```csharp
Console.WriteLine(metadata.Standard);                 // ComicInfo
Console.WriteLine(metadata.PrimarySource?.Location);  // ComicInfo.xml

foreach (var source in metadata.Sources)
{
    Console.WriteLine($"{source.Standard} at {source.Location} (primary: {source.IsPrimary})");
}
```

### Metadata parsing failures

A metadata source that cannot be parsed does not prevent an archive from being opened. Resolution
continues with the next source in precedence order. If no source can be parsed, canonical metadata is
empty and every source located remains listed in `ComicMetadata.Sources`. `ComicValidator` reports the
failure and its cause.

### Archive format detection

`ComicDetector` identifies an archive without opening it as a `ComicBook`.

```csharp
var info = await ComicDetector.DetectAsync("example.cbz");

Console.WriteLine(info.Format);
Console.WriteLine(info.ExtensionMatchesFormat);
Console.WriteLine(info.PageCount);
```

Detection reads the file header and the container index. Page content is never decompressed, so the
cost is proportional to the number of entries rather than to the size of the archive.

`ComicDetectionDepth.Header` limits detection to the archive header. Page count, metadata presence and
encryption are then not reported, and the operation cost is independent of archive size.

```csharp
var options = new ComicDetectionOptions { Depth = ComicDetectionDepth.Header };
var result = await ComicDetector.DetectAsync(path, options);
```

### Validation

`ComicValidator` reports defects as diagnostics rather than exceptions. Findings are classified as
errors, which indicate that the archive cannot be used as a comic, or warnings, which indicate an
anomaly that does not prevent use. Metadata defects are reported as warnings.

```csharp
var result = await ComicValidator.ValidateAsync("example.cbz");

foreach (var issue in result.Issues)
{
    Console.WriteLine($"{issue.Severity} [{issue.Code}]: {issue.Message}");
}
```

`ComicValidationOptions.DeepScan` additionally decompresses every page and verifies that its content
begins with a signature consistent with the image format implied by its file extension.

### Extraction

```csharp
await comic.Pages[0].ExtractAsync("cover.jpg");
await comic.Pages[0].ExtractAsync(destinationStream);
await comic.ExtractAllAsync("./output");
```

`ComicPage.ExtractAsync` writes content directly to the destination without buffering the page in
memory. `ComicBook.ExtractAllAsync` recreates the archive's directory structure under the destination
directory; `ComicExtractionOptions` controls overwriting, flattening and the inclusion of non-image
resources.

Entry names are sanitised and the resolved destination is verified to be contained within the
destination directory, so an entry cannot be written outside it.

### Conversion

```csharp
await using var comic = await ComicBook.OpenAsync("example.cbr");
await comic.ExportAsync("example.cbz", ComicExportFormat.Cbz);
```

Conversion repackages the archive. Page content is copied without re-encoding, entry names and page
order are preserved, and the original metadata file is copied verbatim rather than regenerated from
the canonical model. Archive comments are preserved where the target container supports them. Entries
are processed one at a time, so memory consumption does not scale with archive size.

CBZ is the only supported export target. RAR compression is proprietary and cannot be produced by a
managed implementation.

### Caching

Caching is optional and disabled by default. `ComicCache.InMemory` provides a size-bounded,
least-recently-used cache of extracted entry content.

```csharp
var cache = ComicCache.InMemory(maxSizeInBytes: 128 * 1024 * 1024);

await using var comic = await ComicBook.OpenAsync("example.cbz", new ComicOpenOptions { Cache = cache });
```

A `ComicCache` instance may be shared by multiple `ComicBook` instances, in which case they share a
single budget. Cache entries are keyed by archive path, file size and last write time, so a modified
file does not return previously cached content.

### Resource limits

Archives are treated as untrusted input. Reads are bounded by default and the limits are configurable.

```csharp
var options = new ComicOpenOptions
{
    MaxEntrySizeInBytes = 256L * 1024 * 1024,  // per entry; default 256 MiB
    MaxEntryCount = 50_000,                    // per archive; default 50,000
};
```

Limits are enforced against the number of bytes read, not against the sizes declared by the archive.

## Behaviour and guarantees

### Thread safety

A `ComicBook` instance may be accessed concurrently. Content reads are serialised per instance for
every container format, including those with random access, so concurrent reads are safe but are
executed one at a time. Streams returned by `ComicPage.OpenAsync` and `ComicResource.OpenAsync` are
independent of the instance that produced them and remain valid after it has been disposed.

### Disposal

Disposal lets the read in progress complete. `ComicBook.DisposeAsync` waits for that read before
releasing the archive; `ComicBook.Dispose` returns immediately and the archive is released when the
read finishes. Reads requested after either call, including reads already queued, throw
`ObjectDisposedException`.

### Cancellation

All potentially long-running operations accept a `CancellationToken`. A cancelled export deletes the
partially written destination file. A cancelled extraction leaves files already written in place.

### Image processing

ComiX does not decode image data. Page dimensions are therefore available only when declared by the
source metadata, and image formats are identified from file extensions. `ComicValidationOptions.DeepScan`
verifies image signatures but does not decode image content.

### Double-page spreads

`ComicPage.DeclaredSpread` reports the spread type declared by the source metadata. `ComicInfo.xml` is
the only supported standard that describes pages individually; CoMet, ComicBookInfo and MetronInfo
define a page count only. `DeclaredSpread` is therefore `Unknown` for archives tagged with those
standards, for untagged archives, and for ComicInfo documents that omit the attribute.

```csharp
var layout = page.DeclaredSpread switch
{
    ComicPageSpread.Double => Layout.TwoUp,
    ComicPageSpread.Single => Layout.OneUp,
    // Determine the layout from the aspect ratio when no spread type is declared.
    _ => DetermineLayoutFromAspectRatio(page),
};
```

### Solid archives

7-Zip archives, and RAR archives created in solid mode, store entries in shared compression blocks.
Accessing an individual entry may require decompressing preceding data. ComiX does not eliminate this
constraint imposed by the archive format, and reports it through `ComicCapabilities.RandomAccess`.

```csharp
if (!comic.Capabilities.HasFlag(ComicCapabilities.RandomAccess))
{
    // Sequential access is more efficient for this archive.
}
```

Caching is disabled by default, so every read of a page in a solid archive repeats that
decompression, including reads of pages already visited. Applications that revisit pages, such as
readers, should supply `ComicCache.InMemory` through `ComicOpenOptions.Cache`.

### Multi-volume archives

An archive that forms part of a multi-volume set can be opened and enumerated, but content stored in
the remaining volumes is unavailable. `ComicValidator` reports this condition as an error.

## Limitations

- Metadata cannot be written into an existing archive.
- CBZ is the only supported export target.
- EPUB and PDF are not supported as containers.
- Image dimensions and spread information are limited to what the source metadata declares.
- Encrypted archives require a password supplied through `ComicOpenOptions.Password`; encrypted
  entries are otherwise reported by detection and validation and cannot be read.

## Building from source

The .NET 10 SDK is required. The test suite targets both supported frameworks, so the .NET 8 runtime
must also be installed.

```bash
dotnet build
dotnet test
```

## License

Released under the [MIT License](https://github.com/lzippilli/ComiX/blob/main/LICENSE).
