#Requires -Version 5.1
<#
  Pass the Fear - Thai mod installer.

  Installs BepInEx 6 (IL2CPP) and the Thai plugin into the game folder. Nothing outside
  the game folder is touched and everything it writes is removed by Uninstall.ps1.

  This file MUST stay UTF-8 *with BOM*: Windows PowerShell 5.1 reads .ps1 in the system
  ANSI codepage otherwise, and on a Thai machine every Thai string below would be mangled
  into something that breaks parsing.
#>
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Root = $PSScriptRoot
$BepBuild = 785
$BepHash = '6abdba4'
$BepUrl = "https://builds.bepinex.dev/projects/bepinex_be/$BepBuild/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.$BepBuild%2B$BepHash.zip"

function Write-Step($n, $text) { Write-Host "`n[$n] $text" -ForegroundColor Cyan }
function Write-Ok($text)       { Write-Host "    $text" -ForegroundColor Green }
function Write-Warn($text)     { Write-Host "    $text" -ForegroundColor Yellow }

Write-Host ""
Write-Host "===============================================" -ForegroundColor Magenta
Write-Host "  Pass the Fear - ม็อดภาษาไทย โดย Krirk0137" -ForegroundColor Magenta
Write-Host "===============================================" -ForegroundColor Magenta

# --- 1. find the game -----------------------------------------------------------------
Write-Step 1 "ค้นหาโฟลเดอร์เกม"

function Find-Game {
    # Steam records every library folder here, so this covers games on other drives too.
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

if (-not $GameDir) { $GameDir = Find-Game }

while (-not ($GameDir -and (Test-Path (Join-Path $GameDir 'PassTheFear.exe')))) {
    Write-Warn "หาโฟลเดอร์เกมไม่เจอ"
    Write-Host  "    เปิด Steam คลิกขวาที่เกม -> Manage -> Browse local files"
    Write-Host  "    แล้วคัดลอกที่อยู่โฟลเดอร์มาวางตรงนี้ (โฟลเดอร์ที่มีไฟล์ PassTheFear.exe)"
    $GameDir = (Read-Host "    ที่อยู่โฟลเดอร์เกม").Trim('"', ' ')
    if (-not $GameDir) { throw "ยกเลิกการติดตั้ง" }
}
Write-Ok "เจอแล้ว: $GameDir"

if (Get-Process -Name 'PassTheFear' -EA SilentlyContinue) {
    throw "เกมเปิดอยู่ กรุณาปิดเกมก่อนแล้วรันใหม่"
}

# --- 2. BepInEx -----------------------------------------------------------------------
Write-Step 2 "ติดตั้ง BepInEx"

$marker = Join-Path $GameDir 'BepInEx\.ptf_bepinex_build'
$have = if (Test-Path $marker) { (Get-Content $marker -Raw).Trim() } else { '' }

if ($have -eq "$BepBuild") {
    Write-Ok "ติดตั้งอยู่แล้ว (build $BepBuild) ข้ามขั้นตอนนี้"
} else {
    $zip = Join-Path $Root "BepInEx-be.$BepBuild.zip"
    if (-not (Test-Path $zip)) {
        Write-Host "    กำลังดาวน์โหลด BepInEx 6.0.0-be.$BepBuild (~34 MB) ..."
        try {
            Invoke-WebRequest -Uri $BepUrl -OutFile $zip -UseBasicParsing
        } catch {
            throw "ดาวน์โหลด BepInEx ไม่สำเร็จ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่`n$($_.Exception.Message)"
        }
    }
    # Stale interop from an older build makes the next launch fail.
    foreach ($rel in @('BepInEx\interop', 'BepInEx\cache', 'BepInEx\unity-libs')) {
        $p = Join-Path $GameDir $rel
        if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    }
    Expand-Archive -Path $zip -DestinationPath $GameDir -Force
    Set-Content -Path $marker -Value "$BepBuild" -Encoding ASCII
    Write-Ok "ติดตั้ง BepInEx เรียบร้อย"
}

$cfgDir = Join-Path $GameDir 'BepInEx\config'
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
@"
[Logging.Console]

Enabled = false

[Logging]

UnityLogListening = false
"@ | Set-Content -Path (Join-Path $cfgDir 'BepInEx.cfg') -Encoding UTF8

# --- 3. the mod -----------------------------------------------------------------------
Write-Step 3 "ติดตั้งม็อดภาษาไทย"

$src = Join-Path $Root 'plugin'
$dst = Join-Path $GameDir 'BepInEx\plugins\PassTheFear.Thai'
if (-not (Test-Path $src)) { throw "ไม่พบโฟลเดอร์ plugin - แตกไฟล์ zip ไม่ครบหรือเปล่า" }

New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item (Join-Path $src '*') -Destination $dst -Recurse -Force
$n = (Get-Content (Join-Path $dst 'Thai.tsv') | Where-Object { $_ -and $_ -notmatch '^\s*#' }).Count
Write-Ok "คัดลอกคำแปล $n บรรทัดเรียบร้อย"

Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "  ติดตั้งเสร็จแล้ว" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  เปิดเกมได้เลย " -NoNewline
Write-Host "ครั้งแรกจะโหลดนานกว่าปกติ 1-2 นาที" -ForegroundColor Yellow
Write-Host "  (BepInEx กำลังเตรียมไฟล์ ครั้งต่อไปจะเร็วตามปกติ)"
Write-Host ""
Write-Host "  ถ้าอยากแก้คำแปลเอง เปิดไฟล์นี้ด้วย Notepad:"
Write-Host "    $dst\Thai.tsv" -ForegroundColor Cyan
Write-Host "    แต่ละบรรทัดคือ  รหัส <TAB> คำแปล  -- ห้ามแก้ฝั่งซ้าย บันทึกเป็น UTF-8"
Write-Host ""
Write-Host "  ถ้ามีปัญหา ส่ง log นี้มาได้:"
Write-Host "    $GameDir\BepInEx\LogOutput.log" -ForegroundColor Cyan
Write-Host ""
