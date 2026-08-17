#!/bin/bash
# Rebuild ClassicUs.ManuAPI from source against the *real* Classic Us 8.9
# interop, applying the drift fixes, and repack it into the local feed as
# classicus.manuapi.1.5.2 (so the stock nuget.org 1.5.1 can never silently
# shadow the fixed build).
#
# Requires:
#   - the ManuAPI source clone at /tmp/manuapi (github.com/TechDevOfficial/ClassicUs.ManuAPI)
#   - packages/classicus.gamelibs.2026.7.11.1.nupkg already updated to the real
#     8.9 interop (see tools/update_gamelibs.py + PORTING.md)
#   - the .NET SDK
set -e

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC=/tmp/manuapi
BUILD=/tmp/manuapi-build
FEED="$ROOT/packages"
VERSION=1.5.2

rm -rf "$BUILD"
cp -r "$SRC" "$BUILD"

echo '===== applying reproducible Classic Us 8.9 patches ====='
python3 "$ROOT/tools/patch_manuapi_89.py" "$BUILD/ManuAPI"

echo '===== nuget.config for build ====='
cat > "$BUILD/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="LocalPackages" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

echo '===== building ManuAPI ====='
cd "$BUILD"
dotnet build ManuAPI/ManuAPI.csproj -c Release 2>&1 | tail -8

DLL="$BUILD/ManuAPI/bin/Release/ClassicUs.ManuAPI.dll"
[ -f "$DLL" ] || { echo "ERROR: build output missing"; exit 1; }

echo '===== repacking into local feed ====='
PKG="$FEED/classicus.manuapi.$VERSION.nupkg"
if [ ! -f "$PKG" ]; then
  # update_manuapi.py repacks an *existing* package, so bootstrap the base from
  # the stock nuget.org 1.5.1 (via the nuget cache, falling back to a download).
  echo "base $PKG missing - bootstrapping from stock 1.5.1..."
  STOCK="$HOME/.nuget/packages/classicus.manuapi/1.5.1/classicus.manuapi.1.5.1.nupkg"
  if [ ! -f "$STOCK" ]; then
    curl -sL -o "$PKG" "https://api.nuget.org/v3-flatcontainer/classicus.manuapi/1.5.1/classicus.manuapi.1.5.1.nupkg"
    mv "$PKG" "$STOCK" 2>/dev/null || true
    STOCK="$PKG"
  fi
  cp "$STOCK" "$PKG"
fi
python3 "$ROOT/tools/update_manuapi.py" "$PKG" "$DLL" "$VERSION"

echo '===== done ====='
echo "Now: cd $ROOT && rm -rf ~/.nuget/packages/classicus.manuapi && dotnet restore --force && dotnet build -c Release"
