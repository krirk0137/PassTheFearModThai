"""Build the master translation table from the exported localization XML.

Reads build/loc/<language>/dictionaries/*.xml (produced by loc_export.py) and writes
loc/source.tsv — one row per string, with English and Simplified Chinese side by side.
English is itself a translation of the Chinese original, so keeping both lets the
translator resolve anything the English renders ambiguously.

Columns: file, key, en, zh

Usage:
    python tools/loc_tsv.py [-i build/loc] [-o loc/source.tsv]
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REFERENCE_LANGS = {"en": "english", "zh": "chinesesimplified"}
TSV_HEADER = ["file", "key", "en", "zh"]


def read_dictionary(path: Path) -> dict[str, str]:
    """Return {key: value} for one dictionary XML. Files are UTF-8 with a BOM."""
    root = ET.fromstring(path.read_text(encoding="utf-8-sig"))
    out: dict[str, str] = {}
    for node in root.iter("String"):
        key = node.get("Key")
        if key is None:
            continue
        out[key] = node.get("Value", "")
    return out


def escape(value: str) -> str:
    return value.replace("\\", "\\\\").replace("\t", "\\t").replace("\r", "").replace("\n", "\\n")


def build(loc_dir: Path, out_path: Path) -> tuple[int, int]:
    langs = {
        code: {p.stem: read_dictionary(p) for p in sorted((loc_dir / name / "dictionaries").glob("*.xml"))}
        for code, name in REFERENCE_LANGS.items()
    }

    # English is the ordering authority: it is the set we translate from.
    en_files = langs["en"]
    rows: list[list[str]] = []
    missing_zh = 0

    for stem, entries in sorted(en_files.items()):
        zh = langs["zh"].get(stem, {})
        for key, value in entries.items():
            if key not in zh:
                missing_zh += 1
            rows.append([stem, key, escape(value), escape(zh.get(key, ""))])

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(TSV_HEADER) + "\n")
        for row in rows:
            f.write("\t".join(row) + "\n")

    return len(rows), missing_zh


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("-i", "--loc-dir", type=Path, default=Path("build/loc"))
    ap.add_argument("-o", "--out", type=Path, default=Path("loc/source.tsv"))
    args = ap.parse_args(argv)

    total, missing_zh = build(args.loc_dir, args.out)
    print(f"{total} strings -> {args.out}")
    if missing_zh:
        print(f"  warning: {missing_zh} keys have no Simplified Chinese counterpart")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
