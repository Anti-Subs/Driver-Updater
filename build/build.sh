#!/bin/bash
# Builds DriverUpdaterSetup-<version>.exe on Linux/macOS.
# Needs: PowerShell 7 (pwsh, includes the Roslyn C# compiler) and the .NET Framework 4.8 reference
# assemblies from https://github.com/mono/reference-assemblies (cloned to $REFASM, default /tmp/refasm).
set -e
VERSION="${1:?usage: build.sh 3.4.1}"
cd "$(dirname "$0")/.."
REFASM="${REFASM:-/tmp/refasm}"
[ -d "$REFASM/v4.8" ] || git clone --depth 1 https://github.com/mono/reference-assemblies.git "$REFASM"
mkdir -p out
cat > src/Version.cs <<V
using System.Reflection;
[assembly: AssemblyVersion("$VERSION.0")]
[assembly: AssemblyFileVersion("$VERSION.0")]
[assembly: AssemblyInformationalVersion("$VERSION")]
V
sed -E "s/(assemblyIdentity version=\")[0-9.]+\"/\1$VERSION.0\"/" assets/app.manifest > out/app.manifest
pwsh -NoProfile -File build/compile.ps1 -RefDir "$REFASM/v4.8" \
  -Sources src/Core.cs,src/Vendors.cs,src/VendorNet.cs,src/NvidiaData.cs,src/Changelog.cs,src/Platform.cs,src/Installer.cs,src/Xaml.cs,src/App.cs,src/Version.cs \
  -Out "out/DriverUpdaterSetup-$VERSION.exe" -Icon assets/app.ico -Manifest out/app.manifest -Optimize
