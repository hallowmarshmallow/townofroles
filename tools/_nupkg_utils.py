#!/usr/bin/env python3
"""Shared helpers for rewriting the local-feed nupkgs in packages/.

These packages are repacked locally (GameLibs is malformed upstream; ManuAPI is
rebuilt from source for Classic Us 8.9), which invalidates their NuGet code
signatures. A stale .signature.p7s makes every restore emit NU3005 warnings,
so we strip the signature parts (file + [Content_Types].xml entry + _rels/.rels
relationship) consistently — NuGet then treats the package as simply unsigned.

Rewrite strategy: extract with the `unzip` CLI (it reads the central directory
and tolerates NuGet's zip quirks that python's zipfile rejects), apply changes
to the extracted tree, then re-zip. This avoids python zipfile's strict
local-header validation entirely.
"""
import os
import re
import shutil
import subprocess
import tempfile
import zipfile

CONTENT_TYPE_P7S = re.compile(r'<Default Extension="p7s"[^/]*/>')
SIGNATURE_RELS = re.compile(r'<Relationship[^>]*digital-signature[^>]*/>')
NUSPEC_VERSION = re.compile(r"(<version>)[^<]+(</version>)")


def rewrite_nupkg(src, dst, mutations=None, nuspec_version=None):
    """Extract src nupkg, apply mutations (dict of 'path/in/pkg' -> bytes, or
    None to delete), optionally bump the root *.nuspec <version>, strip the
    stale NuGet signature, and write the result to dst."""
    mutations = mutations or {}
    tmp = tempfile.mkdtemp(prefix="nupkg-")
    try:
        subprocess.run(["unzip", "-o", "-q", src, "-d", tmp], check=True)

        for name, data in mutations.items():
            path = os.path.join(tmp, *name.split("/"))
            if data is None:
                if os.path.exists(path):
                    os.remove(path)
            else:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "wb") as f:
                    f.write(data)

        if nuspec_version:
            for f in os.listdir(tmp):
                if f.endswith(".nuspec"):
                    p = os.path.join(tmp, f)
                    s = open(p, encoding="utf-8").read()
                    s = NUSPEC_VERSION.sub(rf"\g<1>{nuspec_version}\g<2>", s, count=1)
                    open(p, "w", encoding="utf-8").write(s)
                    break

        # Strip stale NuGet signature (file + its two OPC references).
        sig = os.path.join(tmp, ".signature.p7s")
        if os.path.exists(sig):
            os.remove(sig)
        ct = os.path.join(tmp, "[Content_Types].xml")
        if os.path.exists(ct):
            s = open(ct, encoding="utf-8").read()
            open(ct, "w", encoding="utf-8").write(CONTENT_TYPE_P7S.sub("", s))
        rels = os.path.join(tmp, "_rels", ".rels")
        if os.path.exists(rels):
            s = open(rels, encoding="utf-8").read()
            open(rels, "w", encoding="utf-8").write(SIGNATURE_RELS.sub("", s))

        with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as z:
            for dirpath, _, files in os.walk(tmp):
                for name in files:
                    full = os.path.join(dirpath, name)
                    z.write(full, os.path.relpath(full, tmp))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
