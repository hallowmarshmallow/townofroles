#!/bin/bash
# Build the managed stack against the native Classic Us Linux 8.16 interop.
# This intentionally uses packages/linux and a separate NuGet cache so the
# Windows-generated interop/package remains untouched.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="${MANUAPI_SRC:-/tmp/manuapi}"
LINUX_INTEROP="${LINUX_INTEROP:-/tmp/interop89-linux-real/Assembly-CSharp.dll}"
FEED="$ROOT/packages/linux"
CACHE="${NUGET_PACKAGES_LINUX:-/tmp/tou-nuget-linux}"
BUILD="${MANUAPI_BUILD_LINUX:-/tmp/manuapi-build-linux}"
VERSION=1.5.2

[ -d "$SRC" ] || { echo "ERROR: ManuAPI source not found at $SRC"; exit 1; }
[ -f "$LINUX_INTEROP" ] || { echo "ERROR: Linux interop not found at $LINUX_INTEROP"; exit 1; }
[ -f "$ROOT/packages/classicus.gamelibs.2026.8.16.1.nupkg" ] || { echo "ERROR: Windows GameLibs package is missing"; exit 1; }
[ -f "$ROOT/packages/classicus.manuapi.1.5.2.nupkg" ] || { echo "ERROR: fixed ManuAPI 1.5.2 package is missing"; exit 1; }

mkdir -p "$FEED"
cp "$ROOT/packages/classicus.gamelibs.2026.8.16.1.nupkg" "$FEED/classicus.gamelibs.2026.8.16.1.nupkg"
python3 "$ROOT/tools/update_gamelibs.py" "$FEED/classicus.gamelibs.2026.8.16.1.nupkg" "$LINUX_INTEROP"

REACTOR_SOURCE="$HOME/.nuget/packages/classicus.reactor/1.1.0/classicus.reactor.1.1.0.nupkg"
[ -f "$REACTOR_SOURCE" ] || { echo "ERROR: restore/cache ClassicUs.Reactor 1.1.0 first"; exit 1; }
cp "$REACTOR_SOURCE" "$FEED/classicus.reactor.1.1.0.nupkg"

rm -rf "$BUILD"
cp -r "$SRC" "$BUILD"
cat > "$BUILD/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalPackagesLinux" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

cd "$BUILD/ManuAPI"
python3 "$ROOT/tools/patch_manuapi_89.py" "$BUILD/ManuAPI"

rm -rf "$CACHE"
NUGET_PACKAGES="$CACHE" dotnet build ManuAPI.csproj -c Release --configfile "$BUILD/nuget.config" 2>&1 | tail -8
MANUAPI_DLL="$BUILD/ManuAPI/bin/Release/ClassicUs.ManuAPI.dll"
[ -f "$MANUAPI_DLL" ] || { echo "ERROR: Linux ManuAPI build output missing"; exit 1; }

cp "$ROOT/packages/classicus.manuapi.1.5.2.nupkg" "$FEED/classicus.manuapi.1.5.2.nupkg"
python3 "$ROOT/tools/update_manuapi.py" "$FEED/classicus.manuapi.1.5.2.nupkg" "$MANUAPI_DLL" "$VERSION"

cat > /tmp/tou-linux.nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalPackagesLinux" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

cd "$ROOT"
# Keep the SDK's normal intermediate path, but clean it first. A custom
# BaseIntermediateOutputPath can make the SDK compile two generated
# AssemblyInfo files when a prior platform build left obj/Release behind.
rm -rf obj bin/Release/linux
NUGET_PACKAGES="$CACHE" dotnet restore --configfile /tmp/tou-linux.nuget.config --force
NUGET_PACKAGES="$CACHE" dotnet build -c Release --no-restore \
  -p:OutputPath="$ROOT/bin/Release/linux/" 2>&1 | tail -8

[ -f "$ROOT/bin/Release/linux/TownOfUs.ManuAPI.dll" ] || { echo "ERROR: Linux TownOfUs build output missing"; exit 1; }
echo "Linux build complete: $ROOT/bin/Release/linux/TownOfUs.ManuAPI.dll"
echo "Linux packages: $FEED"
