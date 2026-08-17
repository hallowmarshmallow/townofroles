#!/usr/bin/env python3
"""Publish helper for the Town Of Us self-update system.

Builds the mod, computes the DLL SHA-256, writes latest.json, and stages the
two files you upload to a GitHub Release. The mod checks
  https://github.com/OWNER/REPO/releases/latest/download/latest.json
on launch, so use that stable URL in your [Updates] ManifestUrl config.

Usage (Linux):
  export DOTNET_ROOT=/tmp/dotnet
  python3 tools/publish_update.py --version 0.2.0 --notes "Fixed kill crash" \
      --repo-owner MyName --repo-name TownOfUs.ManuAPI

Outputs to /tmp/tou-release-stage/:
  latest.json
  TownOfUs.ManuAPI.dll

Then create a GitHub Release tagged v<version> and upload BOTH files as assets.
The DLL asset must be named TownOfUs.ManuAPI.dll (the mod downloads it by that
name); latest.json can be any name but must be served at the ManifestUrl.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLUGIN_CS = os.path.join(ROOT, "TownOfUsPlugin.cs")
STAGE = "/tmp/tou-release-stage"


def sha256(path, chunk=1024 * 1024):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            block = f.read(chunk)
            if not block:
                break
            h.update(block)
    return h.hexdigest()


def build(dotnet_root):
    env = dict(os.environ)
    env["DOTNET_ROOT"] = dotnet_root
    env["PATH"] = dotnet_root + os.pathsep + env.get("PATH", "")
    cmd = [
        os.path.join(dotnet_root, "dotnet"), "build", "-c", "Release",
        "-p:OutputPath=" + os.path.join(ROOT, "bin", "Release", "publish"),
        os.path.join(ROOT, "TownOfUs.ManuAPI.csproj"),
    ]
    subprocess.run(cmd, env=env, check=True)
    dll = os.path.join(ROOT, "bin", "Release", "publish", "TownOfUs.ManuAPI.dll")
    if not os.path.exists(dll):
        sys.exit("Build did not produce TownOfUs.ManuAPI.dll")
    return dll


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", required=True, help="New mod version, e.g. 0.2.0")
    ap.add_argument("--notes", default="", help="Short changelog shown in the update prompt")
    ap.add_argument("--repo-owner", required=True, help="GitHub owner (user or org)")
    ap.add_argument("--repo-name", required=True, help="GitHub repository name")
    ap.add_argument("--dll-name", default="TownOfUs.ManuAPI.dll", help="DLL asset name on the release")
    ap.add_argument("--dotnet-root", default="/tmp/dotnet")
    ap.add_argument(
        "--download-url",
        default=None,
        help="Override the manifest 'url' field (e.g. http://127.0.0.1:8765/TownOfUs.ManuAPI.dll for a local test host).",
    )
    args = ap.parse_args()

    # The mod compares its baked-in Version constant against the manifest.
    # Bump it so a freshly-updated install stops prompting on the next launch.
    bump_version(args.version)

    if os.path.exists(STAGE):
        shutil.rmtree(STAGE)
    os.makedirs(STAGE)

    dll = build(args.dotnet_root)
    digest = sha256(dll)

    if args.download_url:
        download_url = args.download_url
    else:
        download_url = (f"https://github.com/{args.repo_owner}/{args.repo_name}"
                        f"/releases/latest/download/{args.dll_name}")

    manifest = {
        "version": args.version,
        "notes": args.notes,
        "url": download_url,
        "sha256": digest,
    }

    with open(os.path.join(STAGE, "latest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    shutil.copy(dll, os.path.join(STAGE, args.dll_name))

    print("=" * 70)
    if args.download_url:
        print("Local test host stage complete. Point the mod's [Updates] config here:")
        print(f"  ManifestUrl = {args.download_url.rsplit('/', 1)[0]}/latest.json")
        print(f"  DLL URL     = {args.download_url}")
    else:
        print("Stage complete. Upload BOTH files to a GitHub Release:")
        print(f"  Tag:            v{args.version}")
        print(f"  latest.json ->  {STAGE}/latest.json")
        print(f"  {args.dll_name} ->  {STAGE}/{args.dll_name}")
        print()
        print("Set the mod's [Updates] config so the game finds the manifest:")
        print(f"  ManifestUrl = https://github.com/{args.repo_owner}/{args.repo_name}/releases/latest/download/latest.json")
    print(f"  version: {args.version}")
    print(f"  sha256:  {digest}")
    print("=" * 70)

    # Keep bin/ tidy (the user's builds already accumulate intermediate folders).
    shutil.rmtree(os.path.join(ROOT, "bin", "Release", "publish"), ignore_errors=True)


def bump_version(version):
    """Rewrite the TownOfUsPlugin.Version constant to the new version."""
    with open(PLUGIN_CS, "r", encoding="utf-8") as f:
        src = f.read()
    new_src, count = re.subn(
        r'(public const string Version = ")[^"]+(";)',
        r"\g<1>" + version + r"\g<2>",
        src,
        count=1,
    )
    if count != 1:
        sys.exit("Could not find TownOfUsPlugin.Version constant to bump; aborting.")
    with open(PLUGIN_CS, "w", encoding="utf-8") as f:
        f.write(new_src)
    print(f"Bumped TownOfUsPlugin.Version to {version}")


if __name__ == "__main__":
    main()
