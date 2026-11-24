#!/usr/bin/env python3
"""Pack the mod into a zip that players can extract straight into their Mods folder.

The archive root contains a single FilterMachine/ directory, so unzipping it in
<game>/Mods/ is all a player has to do.
"""

import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DLL = ROOT / "bin" / "Release" / "netstandard2.1" / "FilterMachine.dll"
OUT = ROOT / "dist" / "FilterMachine.zip"
PREFIX = "FilterMachine"


def payload():
    """(source file, path inside the archive) for exactly the files the loader expects."""
    entries = [
        (DLL, "FilterMachine.dll"),
        (ROOT / "manifest.xml", "manifest.xml"),
    ]
    for path in sorted((ROOT / "Textures").rglob("*.png")) + sorted((ROOT / "Localization").glob("*.csv")):
        entries.append((path, path.relative_to(ROOT).as_posix()))
    return entries


def main():
    entries = payload()
    missing = [str(path) for path, _ in entries if not path.is_file()]
    if missing:
        print("missing files, run ./build.sh first:\n  " + "\n  ".join(missing), file=sys.stderr)
        return 1

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as archive:
        for path, name in entries:
            archive.write(path, f"{PREFIX}/{name}")

    print(f"wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size} bytes, {len(entries)} files)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
