#Requires -Version 5.1
<#
  Removes the Thai mod and BepInEx from the game folder.

  Must stay UTF-8 *with BOM* - see the note in Install.ps1.
#>
param([string]$GameDir, [switch]$KeepBepInEx)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Find-Game {
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -EA SilentlyContinue).SteamPath
    $roots = @()
    if ($steam) {
        $roots += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $roots += $m.Groups[1].Value -replace '\\\\', '\'
            }
        }
    }
    $roots += 'C:\Program Files (x86)\Steam'
    foreach ($r in $roots | Select-Object -Unique) {
        $p = Join-Path $r 'steamapps\common\Pass The Fear'
        if (Test-Path (Join-Path $p 'PassTheFear.exe')) { return $p }
    }
    return $null
}

Write-Host ""
Write-Host "  ถอนการติดตั้งม็อดภาษาไทย - Pass the Fear" -ForegroundColor Magenta
Write-Host ""

if (-not $GameDir) { $GameDir = Find-Game }
while (-not ($GameDir -and (Test-Path (Join-Path $GameDir 'PassTheFear.exe')))) {
    Write-Host "  หาโฟลเดอร์เกมไม่เจอ วางที่อยู่โฟลเดอร์ที่มี PassTheFear.exe:" -ForegroundColor Yellow
    $GameDir = (Read-Host "  ที่อยู่").Trim('"', ' ')
    if (-not $GameDir) { throw "ยกเลิก" }
}

if (Get-Process -Name 'PassTheFear' -EA SilentlyContinue) {
    throw "เกมเปิดอยู่ กรุณาปิดเกมก่อน"
}

Write-Host "  โฟลเดอร์เกม: $GameDir"
Write-Host ""

if ($KeepBepInEx) {
    $targets = @('BepInEx\plugins\PassTheFear.Thai')
    Write-Host "  จะลบเฉพาะม็อดไทย เก็บ BepInEx ไว้ (ม็อดอื่นยังใช้ได้)" -ForegroundColor Yellow
} else {
    $targets = @('BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')
    Write-Host "  จะลบทั้งม็อดและ BepInEx ออกจากโฟลเดอร์เกม" -ForegroundColor Yellow
    Write-Host "  (ถ้าใช้ม็อดตัวอื่นอยู่ด้วย ให้กด Ctrl+C แล้วรันใหม่พร้อม -KeepBepInEx)"
}
Write-Host ""

$removed = 0
foreach ($rel in $targets) {
    $p = Join-Path $GameDir $rel
    if (Test-Path $p) {
        Remove-Item -Recurse -Force $p
        Write-Host "    ลบแล้ว: $rel" -ForegroundColor Green
        $removed++
    }
}

Write-Host ""
if ($removed -eq 0) {
    Write-Host "  ไม่พบไฟล์ม็อด - อาจถอนไปแล้ว" -ForegroundColor Yellow
} else {
    Write-Host "  ถอนการติดตั้งเรียบร้อย เกมกลับเป็นปกติแล้ว" -ForegroundColor Green
    Write-Host "  เซฟเกมไม่ได้รับผลกระทบ"
    Write-Host "  ถ้าอยากมั่นใจ 100% ใช้ Verify integrity of game files ใน Steam ได้"
}
Write-Host ""
