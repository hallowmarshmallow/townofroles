#!/usr/bin/env python3
"""Repack ClassicUs.GameLibs nupkg with forward-slash paths.

The package published to nuget.org uses backslash path separators
(ref\\net6.0\\...), which NuGet's asset resolver cannot match, so it ships
zero usable compile assets. This normalizes entry names, drops the invalid
signature and the optional OPC metadata parts, and writes a fixed nupkg.
"""
import sys
import zipfile

src, dst = sys.argv[1], sys.argv[2]

zin = zipfile.ZipFile(src)
zout = zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED)

count = 0
for info in zin.infolist():
    name = info.filename.replace("\\", "/")
    if name.startswith("package/") or name.endswith(".p7s") or name == "[Content_Types].xml":
        continue
    data = zin.read(info.filename)
    zout.writestr(name, data)
    count += 1

zout.close()
zin.close()
print(f"Repacked {count} entries -> {dst}")
