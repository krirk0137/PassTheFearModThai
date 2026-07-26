"""List the inner files of GameFramework GFF containers without decrypting them."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from gf_unpack import parse_gff


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("files", nargs="+", type=Path)
    args = ap.parse_args(argv)

    for path in args.files:
        data = path.read_bytes()
        print(f"== {path.name}  ({len(data):,} B)")
        for name, _off, length in parse_gff(data):
            print(f"   {length:>12,}  {name}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
