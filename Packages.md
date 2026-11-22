
# Packages Used

## Risk Assessment common to all

1. Packages on NuGet are virus scanned by the NuGet site.
2. This package is widely used and is actively maintained.
3. Every runtime dependency listed here is open source, and so is every build and test tool except Microsoft.Testing.Extensions.CodeCoverage. That package is closed source, licensed under the Microsoft Software License Terms (Microsoft .NET Library). It is used only to collect code coverage when the tests run, and is not shipped with SynthDicom or BadDicom.

| Package | Source Code | License | Purpose | Additional Risk Assessment |
| ------- | ------------ | ------- | ------- | -------------------------- |
| SynthEHR.Core | [GitHub](https://github.com/jas88/SynthEHR) | [GPL 3.0](https://www.gnu.org/licenses/gpl-3.0.html) | Handles generating baseline random data (patient date of birth, CHI numbers etc) | |
| CsvHelper | [GitHub](https://github.com/JoshClose/CsvHelper) | [MS-PL / Apache 2.0](https://github.com/JoshClose/CsvHelper/blob/master/LICENSE.txt) | Writes the study/series/image CSV files in Csv output mode | |
| fo-dicom | [GitHub](https://github.com/fo-dicom/fo-dicom) | [MS-PL](https://opensource.org/licenses/MS-PL) | Handles reading/writing dicom tags from dicom datasets | |
| Microsoft.CodeAnalysis.CSharp | [GitHub](https://github.com/dotnet/roslyn) | [MIT](https://opensource.org/licenses/MIT) | Roslyn C# compiler for source generators | |
| Microsoft.CodeAnalysis.Analyzers | [GitHub](https://github.com/dotnet/roslyn-analyzers) | [MIT](https://opensource.org/licenses/MIT) | Code analyzers for source generator development | |
| Microsoft.NETCore.App | [GitHub](https://github.com/dotnet/runtime) | [MIT](https://opensource.org/licenses/MIT) | | .Net Core API |
| DicomTypeTranslation | [GitHub](https://github.com/jas88/DicomTypeTranslation) | [GPL 3.0](https://github.com/jas88/DicomTypeTranslation/blob/master/LICENSE) | Translates DICOM type representations | |
| YamlDotNet | [GitHub](https://github.com/aaubry/YamlDotNet) | [MIT](https://opensource.org/licenses/MIT) | Loading configuration files | |
| Vecc.YamlDotNet.Analyzers.StaticGenerator | [GitHub](https://github.com/aaubry/YamlDotNet) | [MIT](https://opensource.org/licenses/MIT) | Extension to YamlDotNet for static processing | |
| CommandLineParser | [GitHub](https://github.com/commandlineparser/commandline) | [MIT](https://opensource.org/licenses/MIT) | Allows command line arguments for main client application and CLI executables | |
| [Nunit](https://nunit.org/) | [GitHub](https://github.com/nunit/nunit) | [MIT](https://opensource.org/licenses/MIT) | Unit testing | |
| [NUnit.Analyzers](https://nunit.org/) | [GitHub](https://github.com/nunit/nunit.analyzers) | [MIT](https://opensource.org/licenses/MIT) | Unit testing support code | |
| NUnit3TestAdapter | [GitHub](https://github.com/nunit/nunit3-vs-adapter) | [MIT](https://opensource.org/licenses/MIT) | Run unit tests: the NUnit runner for Microsoft Testing Platform, and the adapter for Visual Studio | |
| Microsoft.Testing.Extensions.CodeCoverage | Closed source; documentation and samples on [GitHub](https://github.com/microsoft/codecoverage) | [Microsoft Software License Terms (Microsoft .NET Library)](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CodeCoverage/18.12.0/License) | Code coverage collection for the unit tests; build-standards adds it to test projects | Not open source; the licence allows its use to develop and test applications |
| Microsoft.NET.Test.Sdk | [GitHub](https://github.com/microsoft/vstest/) | [MIT](https://opensource.org/licenses/MIT) | Run unit tests | |
| NunitXml.TestLogger | [GitHub](https://github.com/spekt/nunit.testlogger) | [MIT](https://opensource.org/licenses/MIT) | Report test results in XML syntax | |
| XunitXml.TestLogger | [GitHub](https://github.com/spekt/xunit.testlogger) | [MIT](https://opensource.org/licenses/MIT) | Report test results in XML syntax | |
