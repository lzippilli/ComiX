# Changelog

All notable changes to ComiX are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The section for a version is published as the release notes on GitHub and on nuget.org. A release
cannot be published without a section for its version.

## [Unreleased]

### Added

- `ComicOpenOptions.EntryNameEncoding` and `ComicValidationOptions.EntryNameEncoding`, allowing 
  consumers to specify the encoding used for entry names when an archive does not declare UTF-8. 
  This provides a way to correctly decode names written using legacy code pages that would otherwise 
  produce replacement characters.
- `ComicValidationCode.EntryNameEncodingSuspect`, reported when a decoded entry name contains
  replacement or control characters, which is the observable signature of an encoding mismatch.
- A CI workflow building and testing on Linux, Windows and macOS.

## [1.0.0-preview.2] - 2026-09-16

### Changed

- Disposal semantics are now defined. The read in progress always completes: `ComicBook.DisposeAsync`
  waits for it, and `ComicBook.Dispose` returns immediately and releases the archive once the read
  finishes. Reads requested after either call, including queued reads and reads that would be served
  from a cache, throw `ObjectDisposedException`.
- Documentation states that content reads are serialised per instance for every container format,
  and recommends `ComicCache.InMemory` for applications that revisit pages in solid archives.

### Fixed

- Disposing a `ComicBook` while a read was in progress disposed the semaphore that serialises reads.
  The read in progress then failed with `ObjectDisposedException`, and a read queued behind it was
  never released.

## [1.0.0-preview.1] - 2026-09-16

First public release.

### Added

- Reading of CBZ, CBR, CB7 and CBT archives through a container-independent model: `ComicBook`,
  `ComicPage`, `ComicResource`. The container format is determined from file contents.
- Pages in reading order using numeric-aware sorting, with lazy content access and caller-owned
  streams.
- Metadata normalisation from `ComicInfo.xml`, `MetronInfo.xml`, CoMet and ComicBookInfo into
  `ComicMetadata`, with precedence-based resolution, fallback to the next source when the preferred
  one cannot be parsed, and retention of unmapped fields in `Extensions`.
- `ComicCreditRole`, a role type with well-known values, synonym normalisation and preservation of
  unrecognised roles.
- Per-page information from ComicInfo: `Type`, `DeclaredSpread`, `DeclaredWidth`, `DeclaredHeight`
  and `Bookmark`, matched by entry name when the source provides one and by position otherwise.
- `ComicDetector` with header-only and index-level detection depths.
- `ComicValidator`, reporting defects as diagnostics classified as errors or warnings.
- Extraction of pages, resources and whole archives, with sanitised destination paths.
- Conversion of any supported container to CBZ, copying page content, metadata entries and the
  archive comment verbatim.
- Optional in-memory caching of extracted content through `ComicCache.InMemory`.
- Resource limits enforced against bytes read: `ComicOpenOptions.MaxEntrySizeInBytes` and
  `ComicOpenOptions.MaxEntryCount`.
- Target frameworks `net8.0` and `net10.0`. SharpCompress is the only runtime dependency.

[Unreleased]: https://github.com/lzippilli/ComiX/compare/v1.0.0-preview.2...HEAD
[1.0.0-preview.2]: https://github.com/lzippilli/ComiX/compare/v1.0.0-preview.1...v1.0.0-preview.2
[1.0.0-preview.1]: https://github.com/lzippilli/ComiX/releases/tag/v1.0.0-preview.1
