"""Export the localization XML dictionaries out of a decrypted Localization bundle.

Input is the output of tools/gf_unpack.py on StreamingAssets/Local.dat, i.e.
build/unpacked/Local/Localization.dat (a plain UnityFS AssetBundle).

Usage:
    python tools/loc_export.py [bundle] [-o out_dir]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import UnityPy

CONTAINER_PREFIX = "assets/gamemain/gmresources/localization/"


def export(bundle: Path, out_dir: Path) -> int:
    env = UnityPy.load(str(bundle))

    paths: dict[int, str] = {}
    for obj in env.objects:
        if obj.type.name != "AssetBundle":
            continue
        for name, info in obj.read().m_Container:
            paths[info.asset.m_PathID] = name

    count = 0
    for obj in env.objects:
        if obj.type.name != "TextAsset":
            continue
        data = obj.read()
        name = paths.get(obj.path_id, f"_unmapped/{data.m_Name}")
        rel = name[len(CONTAINER_PREFIX) :] if name.startswith(CONTAINER_PREFIX) else name

        script = data.m_Script
        raw = script.encode("utf-8", "surrogateescape") if isinstance(script, str) else bytes(script)

        dest = out_dir / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(raw)
        count += 1
    return count


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "bundle", nargs="?", type=Path, default=Path("build/unpacked/Local/Localization.dat")
    )
    ap.add_argument("-o", "--out", type=Path, default=Path("build/loc"))
    args = ap.parse_args(argv)

    n = export(args.bundle, args.out)
    print(f"exported {n} assets -> {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
