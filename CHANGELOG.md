# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased]

### Fixed
- BadDicom reports an empty `BadDicom.yaml`, or one that holds only comments, as a configuration error and exits with -1, like any other unusable `BadDicom.yaml`. It crashed with a `NullReferenceException`
- BadDicom reports a UID list file named in `BadDicom.yaml` that it can't read, and exits with -1, like any other unusable `BadDicom.yaml`. It crashed with an `UnauthorizedAccessException`
- BadDicom reports a table template that is empty, holds only comments or has no `Tables` as a template error, and exits with -2 before it connects to the database. It failed later with a `NullReferenceException`, and exit code 3, once it had connected
- BadDicom no longer silently skips a UID list file it isn't allowed to read. It checked for the file with `File.Exists`, which also returns false for a file the caller can't read. It now reads the file, reports any failure other than a missing file, and exits with -1. A UID list file that doesn't exist is still skipped, as documented
- BadDicom no longer silently runs without its configuration when it isn't allowed to read `BadDicom.yaml`, which `File.Exists` reported as missing. It now reads the file, reports the error and exits with -1. Only a `BadDicom.yaml` that doesn't exist means no configuration
- BadDicom reports a table template it isn't allowed to read as a read error, with exit code -2, instead of saying the file doesn't exist, with exit code -1. That came from the same `File.Exists` check
- BadDicom reports a table template path that can't name a file, such as one with a NUL character or one too long for the file system, as not a valid file path. It still exits with -1, as when the template is missing, but no longer says the file doesn't exist


## [2.0.1] - 2026-11-16

### Fixed
- Require `SynthEHR.Core` 3.1.0 (previously 3.0.1). SynthEHR.Core 3.0.0 and 3.0.1 are deprecated: their generated EHR data classes returned corrupted text after any non-ASCII character (most prescribing names and descriptions) and culture-dependent numbers. SynthDicom's own output was not affected, as it only uses SynthEHR's person, CHI and address generation, but applications that also generate SynthEHR datasets now resolve a fixed version.
- 3.1.0 also matches SynthDicom's target frameworks (`net10.0`, `net11.0`)


## [2.0.0] - 2026-11-15

### Changed
- Switch from `HIC.SynthEHR` 2.0.1 to `SynthEHR.Core` 3.0.1 (jas88 fork)
- **BREAKING** (seeded output): for the same `-s` seed, generated `PatientID` (CHI) values differ from earlier versions, because SynthEHR 3.0 fixed off-by-one ranges in its CHI digit generation. All other seeded tag values are unchanged, and seeded runs remain reproducible within this version. DICOM UIDs are still never affected by the seed
- Reference `CsvHelper` 33.0.1 directly (previously only a transitive dependency of `HIC.SynthEHR`)
- **BREAKING**: `DicomDataGenerator.GenerateTestDataRow` now returns `object?[]` (matching SynthEHR.Core 3.0.1); the element is null when no images were generated
- **BREAKING**: SynthDicom now targets `net10.0` and `net11.0` (previously `net9.0`, which reached end of support on 10 November 2026)
- BadDicom now targets `net10.0`
- Pixel data text is now rendered with a built-in 8x8 bitmap font (public domain font8x8) instead of ImageSharp; generated pixel data differs from previous versions
- Frame labels on multi-frame images are drawn on a separate line below the UID
- BadDicom now uses the `DicomTypeTranslation` 5.0.1 package (jas88 fork) instead of `HIC.DicomTypeTranslation` 4.1.5, which also replaces `HIC.FAnsiSql` with `FAnsiSql.Legacy`
- Bump fo-dicom from 5.2.1 to 5.2.6 (required by DicomTypeTranslation 5.0.1)

### Security
- Pin transitive FAnsiSql.Legacy to 3.7.0, which brings patched MySqlConnector 2.6.2 (GHSA-473q-m89c-ghf8), System.Security.Cryptography.Pkcs 10.0.14 (GHSA-846f-f764-76mx) and SQLitePCLRaw 3.0.5 (GHSA-2m69-gcr7-jv3q)
- Remove the redundant `Microsoft.SourceLink.GitHub` package reference (Source Link is built into the .NET 8+ SDK), which drops the build-time `Microsoft.Build.Tasks.Git` 8.0.0 dependency (GHSA-23fw-v26w-5fgq)

### Removed
- SixLabors.ImageSharp and SixLabors.ImageSharp.Drawing dependencies
- Unused internal `BitmapToDicom.ConvertToMultiFrame`
- Transitive `MongoDB.Driver` dependency of BadDicom, and with it `SharpCompress`, `DnsClient`, `Snappier` and `ZstdSharp.Port`

## [1.0.0] - 2025-01-23

### 🎉 First Major Release - Complete Modernization

**BREAKING CHANGES:**
- Project renamed from BadMedicine.Dicom to SynthDicom
- **Package ID changed**: HIC.BadMedicine.Dicom → SynthDicom
- Upgraded to .NET 9 (net9.0)
- All dependencies updated
- NuGet package now published as `SynthDicom` (not HIC.SynthDicom)

### Added
- **Async Streaming**: IAsyncEnumerable<T> support for memory-efficient large dataset generation
- **Source Generators**: CSV-driven compile-time code generation (eliminates 2,053 lines of manual code)
- **ArrayPool<T>**: Memory pooling for 20-30% reduction in GC pressure
- **Comprehensive XML Documentation**: 156+ doc comments for all public APIs
- **Global Usings**: Cleaner code with centralized imports
- **Pattern Matching**: Modern switch expressions throughout
- **Multi-frame DICOM Support**: Generate multi-frame DICOM images from bitmap sequences for dynamic imaging

### Changed
- Target framework: net8.0 → net9.0
- Language features: C# 11 → C# 13
- All nullable reference warnings fixed
- Complete null safety with explicit nullable annotations
- Expression-bodied members for simple methods
- ArgumentNullException.ThrowIfNull for validation
- Collection expressions using [] syntax
- Primary constructors where appropriate
- Span<T> and String.Create for zero-allocation string operations
- Pre-sized collections for better performance
- Build configuration centralized in Directory.Build.props
- TreatWarningsAsErrors enabled

### Performance
- 20-35% memory reduction in hot paths
- 30-40% faster string operations
- 15-20% faster LINQ operations
- ArrayPool reduces GC allocations
- **Binary Search Optimization**: O(log n) weighted random selection for modality, study descriptions, and DescBodyPart (2000+ entries)

### Infrastructure
- CI/CD optimized: CodeQL integrated into test workflow
- Dependabot: weekly updates for nuget, github-actions, dotnet-sdk
- Source generators eliminate manual data maintenance
- GitHub Actions use global.json for SDK version

### Dependencies
- SixLabors.ImageSharp: 3.1.7 → 3.1.11 (security fix NU1902)
- SixLabors.ImageSharp.Drawing: 2.1.5 → 2.1.7
- fo-dicom: 5.2.1
- HIC.SynthEHR: 2.0.1
- Microsoft.CodeAnalysis.CSharp: 4.12.0 (new - source generators)

## [0.1.2] - 2024-10-28

- Bump DicomTypeTranslation from 4.1.3 to 4.1.5
- Bump YamlDotNet from 16.0.0 to 16.1.3

## [0.1.1] - 2024-08-15

- Bump fo-dicom from 5.1.2 to 5.1.3
- Bump DicomTypeTranslation from 4.1.2 to 4.1.3
- Bump SynthEHR from 2.0.0 to 2.0.1
- Bump SixLabors.ImageSharp from 3.1.4 to 3.1.5
- Bump SixLabors.ImageSharp.Drawing from 2.1.3 to 2.1.4
- Bump YamlDotNet from 15.1.6 to 16.0.0

## [0.1.0] - 2024-05-29

- Bugfix: no more 44 o'clock scans
- Pre-build BucketList instead of parsing CSVs
- Target .Net 8 not 6

### Dependencies

- Replace BadMedicine v1.2.1 with SynthEHR v2.0.0
- Bump fo-dicom from 5.0.3 to 5.1.2
- Bump SynthEHR from 1.1.1 to 1.1.2
- Bump DicomTypeTranslation from 4.0.1 to 4.1.1
- Bump SixLabors.ImageSharp from 2.1.3 to 3.1.3
- Bump SixLabors.ImageSharp.Drawing from 1.0.0 to 2.1.2
- Bump Vecc.YamlDotNet.Analyzers.StaticGenerator from 13.4.0 to 13.5.1
- Bump YamlDotNet from 12.0.2 to 15.1.6


## [0.0.16] - 2023-10-04

### Dependencies

- Bump SynthEHR from 1.1.0 to 1.1.1
- Bump SixLabors.ImageSharp.Drawing from 1.0.0-beta15 to 2.0.0
- Bump SixLabors.ImageSharp from 2.1.3 to 3.0.2

## [0.0.15] - 2022-10-31

### Dependencies

- Bump YamlDotNet from 11.2.1 to 12.0.2
- Bump SynthEHR from 1.1.0 to 1.1.1
- Bump SixLabors.ImageSharp.Drawing from 1.0.0-beta14 to 1.0.0-beta15

## [0.0.14] - 2022-07-11

### Dependencies

- Bump DicomTypeTranslation from 4.0.0 to 4.0.1
- Bump SynthEHR from 1.0.1 to 1.1.0
- Bump SixLabors.ImageSharp from 2.1.2 to 2.1.3

## [0.0.13] - 2022-06-02

- Fixed SpiralPitchFactor illegal value of 0.0 [#107](https://github.com/jas88/SynthDicom/issues/107)
- Added support for specifying explicit UIDs to use when generating images
- Added linked statistics for frequency of StudyDescription, SeriesDescription and BodyPartExamined for CT
  - Adds SeriesDescription and BodyPartExamined as new tags now modelled
  - Changes StudyDescriptions to more accurately match real DICOM CT data (includes some blank fields)

## [0.0.12] - 2022-05-18

- Fixed memory leaks generating pixel data when running in linux
- Updated to using ImageSharp for pixel data generation instead of libgdiplus

## [0.0.11] - 2022-03-29

### Dependencies

- Built on .Net 6.0 now
- Update fo-dicom to 5.0.2
- Bump DicomTypeTranslation from 3.0.0 to 4.0.0
- Bump NUnit from 3.13.2 to 3.13.3
- Bump Microsoft.NET.Test.Sdk from 17.0.0 to 17.1.0


## [0.0.10] - 2022-02-17

### Fixed

- Fixed non zero exit code when using `--help` on command line

### Dependencies

- Bump NunitXml.TestLogger from 3.0.91 to 3.0.117
- Bump XunitXml.TestLogger from 3.0.62 to 3.0.70
- Bump YamlDotNet from 9.1.4 to 11.2.1
- Bump Microsoft.NET.Test.Sdk from 16.9.1 to 17.0.0
- Bump NUnit from 3.13.1 to 3.13.2
- Bump NUnit3TestAdapter from 3.17.0 to 4.2.0
- Bump DicomTypeTranslation from 2.3.2 to 3.0.0
- Bump fo-dicom.NetCore from 4.0.7 to 4.0.8

## [0.0.9] - 2021-03-03

### Dependencies

- Remove spurious MathNet.Numerics dependency from nuspec as it caused conflicts with SynthEHR
- Bump DicomTypeTranslation from 2.3.1 to 2.3.2
- Bump System.Drawing.Common from 5.0.1 to 5.0.2

## [0.0.8] - 2021-03-02

### Changes

- Now built and released via Github Actions

### Dependencies

- Bump YamlDotNet from 9.1.1 to 9.1.4
- Bump XunitXml.TestLogger from 2.1.26 to 2.1.45
- Bump System.Drawing.Common from 5.0.0 to 5.0.1
- Bump SynthEHR from 0.1.6 to 1.0.0
- Bump Microsoft.NET.Test.Sdk from 16.8.3 to 16.9.1

## [0.0.7] - 2020-08-18

- Enable SourceLink package
- Bump fo-dicom.NetCore from 4.0.5 to 4.0.6
- Bump DicomTypeTranslation from 2.2.2 to 2.3.1
- Bump YamlDotNet from 8.1.1 to 8.1.2
- Clean remaining LGTM alerts

## [0.0.6] - 2020-05-20

### Changed

- Updated dependencies to latest versions (see [Packages.md](./Packages.md))


## [0.0.5] - 2020-01-30

## Added

- Added direct to database mode for CLI application (`BadDicom` only i.e. not part of the nuget package).

## Changed

- Changed number of images per series to 100 for CT

## [0.0.4] - 2019-10-28

## Changed

- Updated dependencies to latest fo Dicom library 4.0.3

## Fixed

- Fixed dodgy tags in test data generated which results in broken images with fo Dicom 4.0.3

## [0.0.3] - 2019-10-28

### Added 

 - Added Csv mode where by dicom tags are output in full to series/study/instance level CSV files

## [0.0.2] - 2019-07-16

### Added 
 
- Xml Documentation now built/shipped with API

## [0.0.1] - 2019-07-03

### Added 

- Command Line Executable
- Support for CT dicom image generation
- Support for PatientAge, Modality, Address, UIDs, StudyDate/Time
- Support for pixel data / NoPixels flag

[Unreleased]: https://github.com/jas88/SynthDicom/compare/v2.0.1...main
[2.0.1]: https://github.com/jas88/SynthDicom/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/jas88/SynthDicom/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/jas88/SynthDicom/compare/v0.1.2...v1.0.0
[0.1.2]: https://github.com/jas88/SynthDicom/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/jas88/SynthDicom/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/jas88/SynthDicom/compare/v0.0.16...v0.1.0
[0.0.16]: https://github.com/jas88/SynthDicom/compare/v0.0.15...v0.0.16
[0.0.15]: https://github.com/jas88/SynthDicom/compare/v0.0.14...v0.0.15
[0.0.14]: https://github.com/jas88/SynthDicom/compare/v0.0.13...v0.0.14
[0.0.13]: https://github.com/jas88/SynthDicom/compare/v0.0.12...v0.0.13
[0.0.12]: https://github.com/jas88/SynthDicom/compare/v0.0.11...v0.0.12
[0.0.11]: https://github.com/jas88/SynthDicom/compare/v0.0.10...v0.0.11
[0.0.10]: https://github.com/jas88/SynthDicom/compare/v0.0.9...v0.0.10
[0.0.9]: https://github.com/jas88/SynthDicom/compare/v0.0.8...v0.0.9
[0.0.8]: https://github.com/jas88/SynthDicom/compare/v0.0.7...v0.0.8
[0.0.7]: https://github.com/jas88/SynthDicom/compare/v0.0.6...v0.0.7
[0.0.6]: https://github.com/jas88/SynthDicom/compare/v0.0.5...v0.0.6
[0.0.5]: https://github.com/jas88/SynthDicom/compare/v0.0.4...v0.0.5
[0.0.4]: https://github.com/jas88/SynthDicom/compare/v0.0.3...v0.0.4
[0.0.3]: https://github.com/jas88/SynthDicom/compare/5517d7e29aaf3742e91b86288b85f692a063dba4...v0.0.3
[0.0.2]: https://github.com/jas88/SynthDicom/compare/v0.0.1...5517d7e29aaf3742e91b86288b85f692a063dba4
[0.0.1]: https://github.com/jas88/SynthDicom/compare/bdea963df0337e47434c3e72bde7a16a111b99a8...v0.0.1
