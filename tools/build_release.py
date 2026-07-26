"""Assemble the end-user release zip.

The dev installer (plugin/install.ps1) builds from source and needs the .NET SDK. Players
get a prebuilt DLL and a script that only downloads BepInEx and copies files.

    python tools/build_release.py --version 0.1.0

Produces dist/PassTheFear-Thai-by-Krirk0137-v<version>.zip:

    ติดตั้ง.bat            -> release/Install.ps1
    ถอนการติดตั้ง.bat       -> release/Uninstall.ps1
    อ่านก่อน.txt
    Install.ps1 / Uninstall.ps1
    plugin/{PassTheFear.Thai.dll, Thai.tsv, Kanit_sdf}
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PLUGIN = ROOT / "plugin" / "PassTheFear.Thai"
RELEASE = ROOT / "release"
DIST = ROOT / "dist"

# .bat is read in the OEM codepage, so its own bytes stay ASCII; every Thai string the
# player sees lives in the .ps1, which is UTF-8 with a BOM.
BAT_INSTALL = """@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
echo.
pause
"""

BAT_UNINSTALL = """@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
echo.
pause
"""


def read_me(version: str, strings: int) -> str:
    return f"""Pass the Fear - ม็อดภาษาไทย  v{version}
แปลไทยโดย Krirk0137

===========================================================
 วิธีติดตั้ง
===========================================================

 1. ปิดเกมให้เรียบร้อย
 2. แตกไฟล์ zip นี้ไว้ที่ไหนก็ได้ (เดสก์ท็อปก็ได้)
 3. ดับเบิลคลิก  ติดตั้ง.bat
 4. ถ้า Windows เตือน SmartScreen -> More info -> Run anyway
 5. รอจนขึ้นว่า "ติดตั้งเสร็จแล้ว"
 6. เปิดเกมได้เลย

 ครั้งแรกเกมจะโหลดนานกว่าปกติ 1-2 นาที เป็นเรื่องปกติ
 BepInEx กำลังเตรียมไฟล์ ครั้งต่อไปจะเร็วเหมือนเดิม

 ต้องต่ออินเทอร์เน็ตตอนติดตั้งครั้งแรก (ดาวน์โหลด BepInEx ~34 MB)

===========================================================
 วิธีถอน
===========================================================

 ดับเบิลคลิก  ถอนการติดตั้ง.bat
 เกมจะกลับเป็นปกติทันที เซฟเกมไม่ได้รับผลกระทบ

 ม็อดนี้ไม่แก้ไฟล์ของเกมแม้แต่ไบต์เดียว ทุกอย่างเกิดในหน่วยความจำตอนเล่น

===========================================================
 อยากแก้คำแปลเอง
===========================================================

 เปิดไฟล์นี้ด้วย Notepad:
   <โฟลเดอร์เกม>\\BepInEx\\plugins\\PassTheFear.Thai\\Thai.tsv

 แต่ละบรรทัดคือ   รหัส <TAB> คำแปลไทย
 แก้ได้เฉพาะฝั่งขวา ห้ามแตะรหัสฝั่งซ้าย
 บันทึกเป็น UTF-8 แล้วเปิดเกมใหม่

===========================================================
 ที่ยังไม่สมบูรณ์
===========================================================

 - แปลครบ {strings:,} บรรทัดแล้ว แต่ยังไม่ได้เล่นยาว ๆ
   อาจเจอข้อความยาวเกินกล่องอยู่บ้าง
 - ยังไม่ได้ทดสอบโหมด co-op
 - เจอปัญหาแจ้งได้ที่ GitHub Issues

===========================================================

 แจกฟรี ห้ามจำหน่าย (CC BY-NC 4.0)
 แจกต่อ แก้ไข ต่อยอดได้ ขอแค่ให้เครดิตและลิงก์กลับมาที่รีโปนี้
 การบริจาคไม่ถือเป็นการขาย แต่ห้ามเรียกเก็บเงินจากคนที่มาโหลด

 ฟอนต์ Kanit โดย Cadson Demak (SIL Open Font License 1.1)
 ม็อดนี้เป็นงานของแฟนเกม ไม่เกี่ยวข้องกับผู้พัฒนา Pass the Fear

 https://github.com/krirk0137/PassTheFearModThai
"""


def main(argv: list[str]) -> int:
    # The zip lists Thai filenames; a cp1252 console would kill the run at the last line.
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass

    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--version", required=True)
    ap.add_argument("--skip-build", action="store_true")
    args = ap.parse_args(argv)

    if not args.skip_build:
        print("building the plugin...")
        r = subprocess.run(
            ["dotnet", "build", str(PLUGIN), "-c", "Release", "--nologo", "-v", "q"],
            capture_output=True, text=True,
        )
        if r.returncode != 0:
            print(r.stdout[-2000:], r.stderr[-2000:])
            return 1

    dll = PLUGIN / "bin" / "Release" / "net6.0" / "PassTheFear.Thai.dll"
    tsv = PLUGIN / "Thai.tsv"
    font = PLUGIN / "Kanit_sdf"
    for p in (dll, tsv, font, RELEASE / "Install.ps1", RELEASE / "Uninstall.ps1"):
        if not p.exists():
            print(f"missing: {p}")
            return 1

    strings = sum(
        1 for line in tsv.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.startswith("#")
    )

    DIST.mkdir(exist_ok=True)
    out = DIST / f"PassTheFear-Thai-by-Krirk0137-v{args.version}.zip"
    if out.exists():
        out.unlink()

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        z.writestr("ติดตั้ง.bat", BAT_INSTALL.replace("\n", "\r\n"))
        z.writestr("ถอนการติดตั้ง.bat", BAT_UNINSTALL.replace("\n", "\r\n"))
        # BOM + CRLF: Notepad on a Thai machine reads it wrong otherwise.
        z.writestr("อ่านก่อน.txt", "﻿" + read_me(args.version, strings).replace("\n", "\r\n"))
        for ps1 in ("Install.ps1", "Uninstall.ps1"):
            z.write(RELEASE / ps1, ps1)
        z.write(dll, "plugin/PassTheFear.Thai.dll")
        z.write(tsv, "plugin/Thai.tsv")
        z.write(font, "plugin/Kanit_sdf")

    size = out.stat().st_size / 1024 / 1024
    print(f"\n{out}  ({size:.1f} MB, {strings:,} strings)")
    for info in zipfile.ZipFile(out).infolist():
        print(f"   {info.file_size:>9,}  {info.filename}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
