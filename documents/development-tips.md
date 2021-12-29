# Development tips

## Quick start

These environment and build steps were tested on a Windows 11 PC and macOS Catalina laptop. Updated 2021-12-28.

We expect the overall process to remain the same; however some steps may shift slightly over time. We'll keep this updated to the best of our ability.

### Prerequisites

* .NET Core SDK 6
  * 6.0.101 at the time of this writing
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
    * `brew cask install dotnet-sdk`
      * (not sure how to install a specific version with this command -- cross your fingers that future versions are backward compatible)

## Build

* Debug
  * `dotnet build`
    * Yep, that's it!
  * Artifacts are dumped to `MBBSEmu\bin\Debug\netcoreapp5.0`
* Release
  * `dotnet build --configuration Release`
  * Artifacts are dumped to `MBBSEmu\bin\Release\netcoreapp5.0`

## Run the tests

* `dotnet test`
  * That should be it!
