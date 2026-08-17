#!/usr/bin/env python3
"""Replace ClassicUs.ManuAPI.dll inside a local ManuAPI nupkg with a freshly
built one and bump the package version (so the 8.9-compatible rebuild is
explicit and can never be silently shadowed by the stock nuget.org 1.5.1).

Usage:
    python3 tools/update_manuapi.py packages/classicus.manuapi.1.5.2.nupkg \\
        /path/to/ClassicUs.ManuAPI.dll 1.5.2

The rebuilt DLL keeps assembly version 1.5.1 (BepInEx/other mods don't care);
only the *package* version changes. The stale NuGet signature is stripped.
"""
import os
import shutil
import sys

from _nupkg_utils import rewrite_nupkg

PKG = sys.argv[1]
DLL = sys.argv[2]
VERSION = sys.argv[3] if len(sys.argv) > 3 else "1.5.2"

with open(DLL, "rb") as f:
    dll_data = f.read()

tmp = PKG + ".tmp"
rewrite_nupkg(PKG, tmp,
              mutations={"lib/net6.0/ClassicUs.ManuAPI.dll": dll_data},
              nuspec_version=VERSION)
shutil.move(tmp, PKG)
print(f"Updated {PKG}: ClassicUs.ManuAPI.dll <- {DLL} ({len(dll_data)} bytes), version {VERSION}, signature stripped")
