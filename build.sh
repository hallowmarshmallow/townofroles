#!/usr/bin/env bash
#
# One-command build for the merged monorepo.
#
# Layout:
#   Networking/  -> ClassicUs.Manactor.dll   (RPC/lobby framework, loads first)
#   API/         -> ClassicUs.MarshAPI.dll   (modding SDK: roles, abilities, kills)
#   ./           -> TownOfUs.ManuAPI.dll     (the role mod itself)
#
# Output: all three DLLs staged in dist/plugins/, ready to drop into
# <game>/BepInEx/plugins/. Remove any previously installed ClassicUs.ManuAPI.dll
# first — MarshAPI carries the same BepInEx GUID (classicus.manuapi) and both
# must never be installed together.
#
# Usage:  sh build.sh [Release|Debug]

set -eu

CONFIG="${1:-Release}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# Locate dotnet (plain PATH first, then the local toolchain install).
DOTNET="${DOTNET:-}"
if [ -z "$DOTNET" ]; then
  if command -v dotnet >/dev/null 2>&1; then
    DOTNET="dotnet"
  elif [ -x /tmp/dotnet/dotnet ]; then
    DOTNET=/tmp/dotnet/dotnet
    export DOTNET_ROOT=/tmp/dotnet
  else
    echo "error: no dotnet SDK found (set DOTNET=/path/to/dotnet)" >&2
    exit 1
  fi
fi
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "==> [1/3] Networking/Manactor.csproj"
"$DOTNET" build Networking/Manactor.csproj -c "$CONFIG"

echo "==> [2/3] TownOfUs.ManuAPI.csproj (builds vendored API/ via ProjectReference)"
"$DOTNET" build TownOfUs.ManuAPI.csproj -c "$CONFIG"

echo "==> [3/3] Staging dist/plugins/"
mkdir -p dist/plugins
cp "Networking/bin/$CONFIG/ClassicUs.Manactor.dll" dist/plugins/
cp "API/bin/$CONFIG/ClassicUs.MarshAPI.dll"        dist/plugins/
cp "bin/$CONFIG/TownOfUs.ManuAPI.dll"              dist/plugins/

echo
echo "Done. Install contents of dist/plugins/ into <game>/BepInEx/plugins/:"
ls -la dist/plugins/
