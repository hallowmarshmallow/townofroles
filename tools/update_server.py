#!/usr/bin/env python3
"""Local update host that pulls directly from the platform build output.

Serves the actual build folder (default bin/Release/linux for Linux,
bin/Release for Windows) and generates latest.json on the fly:

  - version  is read from the TownOfUsPlugin.cs Version constant,
  - sha256   is computed live from the DLL sitting in that folder,
  - url      is derived from the request's Host header, so it works for
             http://127.0.0.1:<port>, http://<lan-ip>:<port>, etc.

No staging step: rebuild the mod, and the server immediately serves the new
DLL with a matching hash.

Usage:
  python3 tools/update_server.py [--port 8765] [--platform linux|windows]

Then set in BepInEx/config/TownOfUs.ManuAPI.cfg:
  [Updates]
  ManifestUrl = http://127.0.0.1:8765/latest.json
"""
import argparse
import hashlib
import http.server
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLUGIN_CS = os.path.join(ROOT, "TownOfUsPlugin.cs")
DLL_NAME = "TownOfUs.ManuAPI.dll"

# Canonical build-output locations used by package_install_zip.py.
PLATFORM_DIRS = {
    "linux": os.path.join(ROOT, "bin", "Release", "linux"),
    "windows": os.path.join(ROOT, "bin", "Release"),
}


def read_version():
    with open(PLUGIN_CS, encoding="utf-8") as f:
        m = re.search(r'Version = "([^"]+)"', f.read())
    if not m:
        sys.exit("Could not find TownOfUsPlugin.Version; aborting.")
    return m.group(1)


def sha256(path, chunk=1024 * 1024):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            block = f.read(chunk)
            if not block:
                break
            h.update(block)
    return h.hexdigest()


class UpdateHandler(http.server.SimpleHTTPRequestHandler):
    server_version = "TownOfUs.UpdateServer/1.0"

    def do_GET(self):
        if self.path.split("?", 1)[0].rstrip("/") == "/latest.json":
            self.serve_manifest()
            return
        super().do_GET()

    def serve_manifest(self):
        dll_path = os.path.join(self.directory, DLL_NAME)
        if not os.path.isfile(dll_path):
            self.send_error(404, f"{DLL_NAME} not found in {self.directory}")
            return

        host = self.headers.get("Host", "127.0.0.1")
        manifest = {
            "version": read_version(),
            "notes": "Local build served from the release output folder.",
            "url": f"http://{host}/{DLL_NAME}",
            "sha256": sha256(dll_path),
        }
        body = json.dumps(manifest, indent=2).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt, *args):
        sys.stdout.write("[%s] %s\n" % (self.log_date_time_string(), fmt % args))
        sys.stdout.flush()


def main():
    ap = argparse.ArgumentParser(description="Local Town Of Us update host")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument(
        "--platform",
        choices=list(PLATFORM_DIRS),
        default="linux",
        help="Which build output folder to serve (default: linux).",
    )
    ap.add_argument(
        "--dir",
        default=None,
        help="Explicit serve directory (overrides --platform).",
    )
    args = ap.parse_args()

    root = os.path.abspath(args.dir) if args.dir else PLATFORM_DIRS[args.platform]
    if not os.path.isdir(root):
        sys.exit(f"ERROR: {root} does not exist. Build the mod first "
                 f"(bin/Release/{args.platform}).")

    os.chdir(root)
    version = read_version()

    host, port = "0.0.0.0", args.port
    httpd = http.server.ThreadingHTTPServer((host, port), UpdateHandler)

    print("=" * 66)
    print(f"Serving build output directly: {root}")
    print(f"  Mod version:  v{version}")
    print(f"  Listening on  0.0.0.0:{port}")
    print(f"  Manifest:     http://127.0.0.1:{port}/latest.json")
    print(f"  DLL:          http://127.0.0.1:{port}/{DLL_NAME}")
    print()
    print("Set the mod's [Updates] config (then restart the game):")
    print(f"  ManifestUrl = http://127.0.0.1:{port}/latest.json")
    print("=" * 66)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        httpd.server_close()


if __name__ == "__main__":
    main()
