"""Validate translated files against their source before they reach the game.

A wrong key is silent — the string just stays English — and a dropped placeholder crashes
string.Format at runtime, in whatever menu happens to use it. Both are much cheaper to catch
here than in game.

    python tools/loc_check.py                 # every file in loc/th
    python tools/loc_check.py gameui c_item   # named files only
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

# {0}, {0:N0}, {1:P0}, <color=#RRGGBB>, </color>, <size=24>, <link="9034">, \n
TOKEN = re.compile(r"\{[^}]*\}|<[^>]*>|\\n|\\t")


def rows(path: Path, columns: int) -> list[list[str]]:
    out = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) < columns:
            parts += [""] * (columns - len(parts))
        out.append(parts)
    return out


def check(name: str, source_dir: Path, th_dir: Path) -> list[str]:
    src_path = source_dir / f"{name}.tsv"
    th_path = th_dir / f"{name}.tsv"

    if not th_path.exists():
        return [f"{name}: not translated yet"]

    src = rows(src_path, 3)
    th = rows(th_path, 2)
    problems: list[str] = []

    src_keys = [r[0] for r in src]
    th_keys = [r[0] for r in th]

    missing = [k for k in src_keys if k not in set(th_keys)]
    extra = [k for k in th_keys if k not in set(src_keys)]
    if missing:
        problems.append(f"{name}: {len(missing)} keys missing, first: {missing[:3]}")
    if extra:
        problems.append(f"{name}: {len(extra)} keys not in the source, first: {extra[:3]}")

    th_map = {r[0]: r[1] for r in th}
    for key, english, _zh in ((r[0], r[1], r[2]) for r in src):
        value = th_map.get(key)
        if value is None:
            continue

        if not value.strip():
            problems.append(f"{name}/{key}: empty value")
            continue

        want = sorted(TOKEN.findall(english))
        got = sorted(TOKEN.findall(value))
        if want != got:
            problems.append(f"{name}/{key}: placeholders differ — source {want}, thai {got}")

    # A raw tab inside a value silently truncates the string when the plugin parses it.
    for i, line in enumerate(th_path.read_text(encoding="utf-8").splitlines(), 1):
        if not line or line.startswith("#"):
            continue
        if line.count("\t") != 1:
            problems.append(f"{name}: line {i} has {line.count(chr(9))} tabs, expected 1")

    raw = th_path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        problems.append(f"{name}: file has a UTF-8 BOM; the plugin expects none")
    if b"\r\n" in raw:
        problems.append(f"{name}: file has CRLF line endings; expected LF")

    return problems


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("names", nargs="*")
    ap.add_argument("--source-dir", type=Path, default=Path("loc/source"))
    ap.add_argument("--th-dir", type=Path, default=Path("loc/th"))
    args = ap.parse_args(argv)

    names = args.names or sorted(p.stem for p in args.th_dir.glob("*.tsv"))
    if not names:
        print("nothing translated yet")
        return 0

    total = 0
    for name in names:
        problems = check(name, args.source_dir, args.th_dir)
        count = len(rows(args.th_dir / f"{name}.tsv", 2)) if (args.th_dir / f"{name}.tsv").exists() else 0
        status = "OK" if not problems else f"{len(problems)} problem(s)"
        print(f"{count:>5} rows  {name:<24} {status}")
        for p in problems[:12]:
            print(f"        {p}")
        if len(problems) > 12:
            print(f"        ... and {len(problems) - 12} more")
        total += len(problems)

    print(f"\n{total} problem(s) total")
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
