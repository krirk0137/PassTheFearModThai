"""Unpack GameFramework (.dat) resources from Pass the Fear.

Two container shapes exist in StreamingAssets:

  * "GFF" virtual file systems (Local.dat, UI.dat, Font.dat, ...) that hold
    one or more inner files.
  * bare per-resource files (GameMain/GMResources/**/*.dat).

In both cases the payload is a stock Unity AssetBundle XOR'd with a 4-byte
key (GameFramework's per-resource hash code). The key is recovered from the
known "UnityFS\\0" magic instead of parsing GameFrameworkVersion.dat.

Usage:
    python tools/gf_unpack.py <file.dat> [-o out_dir]
    python tools/gf_unpack.py <dir> -o out_dir      # walks *.dat
"""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

GFF_MAGIC = b"GFF"
CLUSTER_SIZE = 4096
STRING_ENTRY_SIZE = 256
FILE_ENTRY_SIZE = 12
BUNDLE_MAGIC = b"UnityFS\0"


def xor(data: bytes, key: bytes, start_index: int = 0) -> bytes:
    n = len(key)
    return bytes(b ^ key[(i + start_index) % n] for i, b in enumerate(data))


def derive_key(payload: bytes) -> bytes:
    """Recover the 4-byte XOR key from the first 8 plaintext bytes."""
    guess = bytes(payload[i] ^ BUNDLE_MAGIC[i] for i in range(4))
    if xor(payload[:8], guess) != BUNDLE_MAGIC:
        raise ValueError("payload is not an XOR'd UnityFS bundle")
    return guess


def parse_gff(data: bytes) -> list[tuple[str, int, int]]:
    """Return [(name, offset, length)] for each inner file of a GFF system."""
    if data[:3] != GFF_MAGIC:
        raise ValueError("not a GFF file system")
    encrypt = data[4:8]
    max_file_count, max_block_count, block_count = struct.unpack_from("<iii", data, 8)

    entries = []
    string_base = 20 + FILE_ENTRY_SIZE * max_block_count
    for i in range(block_count):
        string_index, cluster_index, length = struct.unpack_from(
            "<iii", data, 20 + i * FILE_ENTRY_SIZE
        )
        s = string_base + string_index * STRING_ENTRY_SIZE
        name_len = data[s]
        name = xor(data[s + 1 : s + 1 + name_len], encrypt).decode("utf-8")
        entries.append((name, cluster_index * CLUSTER_SIZE, length))
    return entries


def unpack_file(path: Path, out_dir: Path) -> list[Path]:
    data = path.read_bytes()
    written = []

    if data[:3] == GFF_MAGIC:
        members = parse_gff(data)
    else:
        members = [(path.stem + ".bundle", 0, len(data))]

    for name, offset, length in members:
        payload = data[offset : offset + length]
        key = derive_key(payload)
        dest = out_dir / path.stem / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(xor(payload, key))
        written.append(dest)
        print(f"  {name}  {length:>10,} B  key={key.hex()}")
    return written


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("target", type=Path)
    ap.add_argument("-o", "--out", type=Path, default=Path("build/unpacked"))
    args = ap.parse_args(argv)

    targets = (
        sorted(args.target.rglob("*.dat")) if args.target.is_dir() else [args.target]
    )
    for t in targets:
        print(t.name)
        try:
            unpack_file(t, args.out)
        except ValueError as e:
            print(f"  skipped: {e}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
