# Development tips

## Quick start

These environment and build steps were tested on a Windows 11 PC and macOS Ventura laptop. Updated 2023-01-28.

We expect the overall process to remain the same; however some steps may shift slightly over time. We'll keep this updated to the best of our ability.

### Prerequisites

* .NET Core SDK
  * 7.0.102 at the time of this writing
  * x64 was used; not sure about other versions
  * Ubuntu Linux 21.10
    * Following [Microsoft's installation instructions](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#2110-)
    * Install Microsoft package repository

```
wget https://packages.microsoft.com/config/ubuntu/21.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
```

* Install .net sdk

```
sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-6.0
```
  * Windows
    * Installed via [chocolatey](https://chocolatey.org/) - [chocolately .NET Core SDK](https://chocolatey.org/packages/dotnetcore-sdk)
    * `choco install dotnetcore-sdk`
  * macOS
    * Installed via [Homebrew](https://brew.sh) - [Homebrew .NET Core SDK](https://formulae.brew.sh/cask/dotnet-sdk)
    * `brew install --cask dotnet-sdk`
      * (not sure how to install a specific version with this command -- cross your fingers that future versions are backward compatible)

## Build

* Debug
  * `dotnet build`
    * Yep, that's it!
  * Artifacts are dumped to `MBBSEmu\bin\Debug\net7.0`
    * The last directory name may change based on major .net version changes
* Release
  * `dotnet build --configuration Release`
  * Artifacts are dumped to `MBBSEmu\bin\Release\net7.0`
    * The last directory name may change based on major .net version changes

## Run the tests

* `dotnet test`
  * That should be it!
