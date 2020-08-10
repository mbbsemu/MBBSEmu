# Development tips

## Quick start

These environment and build steps were tested on a Windows 10 PC and updated 2020-08-09.

We expect the overall process to remain the same; however some steps may shift slightly over time. We'll keep this updated to the best of our ability.

### Prerequisites

* .NET Core SDK 3.1
  * 3.1.302 at the time of this writing
  * x64 was used; not sure about other versions
  * Installed via [chocolatey](https://chocolatey.org/) - [chocolately .NET Core SDK](https://chocolatey.org/packages/dotnetcore-sdk)
  * `choco install dotnetcore-sdk --version 3.1.302`

## Build

* Debug
  * `dotnet build`
    * Yep, that's it!
  * Artifacts are dumped to `MBBSEmu\bin\Debug\netcoreapp3.1`
* Release
  * `dotnet build --configuration Release`
  * Artifacts are dumped to `MBBSEmu\bin\Release\netcoreapp3.1`
