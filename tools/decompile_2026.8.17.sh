#!/bin/bash
# =============================================================================
# decompile_2026.8.17.sh — Decompile Classic Us 2026.8.17 Linux IL2CPP build
# =============================================================================
#
# Extracts Classic.Us.2026.8.17.Linux.zip and runs the full Cpp2IL →
# Il2CppInterop pipeline to produce a compile-ready Assembly-CSharp.dll
# plus all managed stub DLLs.
#
# OUTPUT:
#   /tmp/decompile-8.17/interop-real/Assembly-CSharp.dll  — compile-ready interop
#   /tmp/decompile-8.17/cpp2il-output/                    — raw Cpp2IL dummy DLLs
#   /tmp/decompile-8.17/cpp2il-output/DummyDll/           — all managed stubs
#
# PREREQUISITES:
#   - .NET SDK 8.0+ (dotnet)
#   - Python 3
#   - Git
#   - Classic.Us.2026.8.17.Linux.zip in the project root
#
# USAGE:
#   bash tools/decompile_2026.8.17.sh
#
# PIPELINE (industry standard for BepInEx IL2CPP modding):
#
#   GameAssembly.so + global-metadata.dat
#       │
#       ▼
#   Cpp2IL  ──►  .NET DLL stubs (dummydll mode)
#       │
#       ▼
#   Il2CppInterop CLI  ──►  Assembly-CSharp.dll (compile-ready)
#       │   - Renames <X>d__NN → _X_d__NN
#       │   - Adds [ObfuscatedName] attributes
#       │   - Generates Il2Cpp wrappers
# =============================================================================
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ZIP="$ROOT/../Classic.Us.2026.8.17.Linux.zip"
WORKDIR="${WORKDIR:-/tmp/decompile-8.17}"
CPP2IL_DIR="$WORKDIR/Cpp2IL"
IL2CPPINTEROP_DIR="$WORKDIR/Il2CppInterop"
CPP2IL_OUT="$WORKDIR/cpp2il-output"
INTEROP_OUT="$WORKDIR/interop-real"
EXTRACT_DIR="$WORKDIR/game-extracted"
GAME_DIR="$EXTRACT_DIR/Classic Us 2026.8.17 Linux"

echo "=============================================="
echo " Classic Us 2026.8.17 — IL2CPP Decompile"
echo "=============================================="
echo ""

# --------------- validate prerequisites ---------------
command -v dotnet >/dev/null 2>&1 || { echo "ERROR: dotnet not found. Install .NET 8.0 SDK."; exit 1; }
command -v git >/dev/null 2>&1 || { echo "ERROR: git not found."; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "ERROR: python3 not found."; exit 1; }

if [ ! -f "$ZIP" ]; then
    echo "ERROR: $ZIP not found."
    exit 1
fi

# --------------- step 1: extract the game zip ---------------
echo "===== [1/5] Extracting game zip ====="
mkdir -p "$EXTRACT_DIR"
unzip -o "$ZIP" -d "$EXTRACT_DIR" 2>&1 | tail -3

if [ ! -f "$GAME_DIR/GameAssembly.so" ]; then
    echo "ERROR: GameAssembly.so not found after extraction in $GAME_DIR"
    exit 1
fi
if [ ! -f "$GAME_DIR/Classic Us 2026.8.17_Data/il2cpp_data/Metadata/global-metadata.dat" ]; then
    echo "ERROR: global-metadata.dat not found"
    exit 1
fi

echo "Game binary:  $GAME_DIR/GameAssembly.so"
echo "Metadata:     $GAME_DIR/Classic Us 2026.8.17_Data/il2cpp_data/Metadata/global-metadata.dat"
echo ""

mkdir -p "$WORKDIR" "$CPP2IL_OUT" "$INTEROP_OUT"

# --------------- step 2: build Cpp2IL ---------------
echo "===== [2/5] Cloning + building Cpp2IL ====="
if [ ! -d "$CPP2IL_DIR" ]; then
    git clone --depth 1 https://github.com/SamboyCoding/Cpp2IL.git "$CPP2IL_DIR" 2>&1 | tail -3
else
    echo "Already cloned ($CPP2IL_DIR). Reusing."
fi

pushd "$CPP2IL_DIR/Cpp2IL" >/dev/null
export DOTNET_ROLL_FORWARD=LatestMajor
dotnet publish -c Release -f net10.0 -o "$WORKDIR/cpp2il-built" 2>&1 | tail -5
popd >/dev/null

CPP2IL_BIN="$WORKDIR/cpp2il-built/Cpp2IL.dll"
[ -f "$CPP2IL_BIN" ] || { echo "ERROR: Cpp2IL build failed"; exit 1; }
echo "Cpp2IL built: $CPP2IL_BIN"

# --------------- step 3: build Il2CppInterop CLI ---------------
echo ""
echo "===== [3/5] Cloning + building Il2CppInterop CLI ====="
if [ ! -d "$IL2CPPINTEROP_DIR" ]; then
    git clone --depth 1 https://github.com/BepInEx/Il2CppInterop.git "$IL2CPPINTEROP_DIR" 2>&1 | tail -3
else
    echo "Already cloned ($IL2CPPINTEROP_DIR). Reusing."
fi

pushd "$IL2CPPINTEROP_DIR" >/dev/null
export DOTNET_ROLL_FORWARD=LatestMajor
if [ -d "CLI/Il2CppInterop.CLI" ]; then
    dotnet publish "CLI/Il2CppInterop.CLI/Il2CppInterop.CLI.csproj" -c Release -o "$WORKDIR/interop-built" 2>&1 | tail -5
elif [ -d "Il2CppInterop.CLI" ]; then
    dotnet publish "Il2CppInterop.CLI/Il2CppInterop.CLI.csproj" -c Release -o "$WORKDIR/interop-built" 2>&1 | tail -5
else
    echo "ERROR: Cannot find Il2CppInterop.CLI project"
    find "$IL2CPPINTEROP_DIR" -name "*.csproj" | head -10
    exit 1
fi
popd >/dev/null

INTEROP_BIN="$WORKDIR/interop-built/Il2CppInterop.CLI.dll"
[ -f "$INTEROP_BIN" ] || { echo "ERROR: Il2CppInterop CLI build failed"; exit 1; }
echo "Il2CppInterop CLI built: $INTEROP_BIN"

# --------------- step 4: run Cpp2IL (dummydll mode) ---------------
echo ""
echo "===== [4/5] Running Cpp2IL (dummydll mode) ====="
echo "This decompiles GameAssembly.so → .NET DLL stubs (~30-120 sec)..."
rm -rf "$CPP2IL_OUT"

dotnet "$CPP2IL_BIN" \
    --game-path "$GAME_DIR" \
    --output-as dummydll \
    --output-to "$CPP2IL_OUT" 2>&1

# Locate the output DLL directory
CPP2IL_DLL_DIR=$(find "$CPP2IL_OUT" -type d -name "DummyDll" | head -1)
if [ -z "$CPP2IL_DLL_DIR" ]; then
    CPP2IL_DLL_DIR=$(find "$CPP2IL_OUT" -name "Assembly-CSharp.dll" -printf '%h\n' -quit)
fi
if [ -z "$CPP2IL_DLL_DIR" ]; then
    echo "WARNING: Could not auto-detect Cpp2IL output directory."
    echo "Contents of $CPP2IL_OUT:"
    find "$CPP2IL_OUT" -name "*.dll" | head -20
    echo ""
else
    echo ""
    echo "Cpp2IL output: $CPP2IL_DLL_DIR"
    echo "DLL count:     $(find "$CPP2IL_DLL_DIR" -name '*.dll' | wc -l)"
fi

# --------------- step 5: run Il2CppInterop generator ---------------
echo ""
echo "===== [5/5] Running Il2CppInterop generator ====="
CPP2IL_INPUT_DIR="${CPP2IL_DLL_DIR:-$CPP2IL_OUT}"

rm -rf "$INTEROP_OUT"
dotnet "$INTEROP_BIN" generate \
    --input "$CPP2IL_INPUT_DIR" \
    --output "$INTEROP_OUT" \
    --game-assembly "$GAME_DIR/GameAssembly.so" 2>&1

INTEROP_DLL="$INTEROP_OUT/Assembly-CSharp.dll"
if [ -f "$INTEROP_DLL" ]; then
    SIZE=$(stat --printf="%s" "$INTEROP_DLL")
    echo ""
    echo "=============================================="
    echo " DECOMPILE COMPLETE"
    echo "=============================================="
    echo ""
    echo " Compile-ready interop:"
    echo "   $INTEROP_DLL ($SIZE bytes)"
    echo ""
    echo " Raw Cpp2IL dummy DLLs:"
    echo "   $CPP2IL_DLL_DIR"
    echo "   ($(find "$CPP2IL_DLL_DIR" -name '*.dll' | wc -l) DLLs)"
    echo ""
    echo "=============================================="
    echo " Next steps (from project root):"
    echo ""
    echo "   # Swap into the GameLibs feed:"
    echo "   python3 tools/update_gamelibs.py \\"
    echo "       packages/classicus.gamelibs.2026.7.11.1.nupkg \\"
    echo "       $INTEROP_DLL"
    echo ""
    echo "   # Rebuild ManuAPI + the mod:"
    echo "   bash tools/build_manuapi.sh"
    echo "   rm -rf ~/.nuget/packages/classicus.gamelibs ~/.nuget/packages/classicus.manuapi"
    echo "   dotnet restore --force && dotnet build -c Release"
    echo ""
else
    echo "ERROR: $INTEROP_DLL was not generated."
    exit 1
fi

echo "Done."