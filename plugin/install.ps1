#Requires -Version 5.1
<#
.SYNOPSIS
  Install BepInEx 6 IL2CPP + the Thai plugin into the game, from scratch.

.DESCRIPTION
  Idempotent. Re-run it after a game reinstall, a Steam update, or any plugin change.
  Nothing outside the game folder is touched, and everything it writes is removable
  with -Uninstall.

  BepInEx Bleeding Edge build 785 is pinned deliberately: metadata v31 (Unity 2022.3)
  is not supported by the stable 6.0.0 releases.

.EXAMPLE
  .\plugin\install.ps1
  .\plugin\install.ps1 -GameDir 'D:\Games\Pass The Fear'
  .\plugin\install.ps1 -SkipBuild        # reuse the last compiled DLL
  .\plugin\install.ps1 -Uninstall
#>
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Pass The Fear',
    [switch]$SkipBuild,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path $PSScriptRoot -Parent
$PluginName = 'PassTheFear.Thai'
$Project    = Join-Path $PSScriptRoot "$PluginName\$PluginName.csproj"
$PluginDir  = Join-Path $GameDir "BepInEx\plugins\$PluginName"

$BepBuild = 785
$BepHash  = '6abdba4'
$BepZip   = Join-Path $RepoRoot 'build\.cache\bepinex6.zip'
$BepUrl   = "https://builds.bepinex.dev/projects/bepinex_be/$BepBuild/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.$BepBuild%2B$BepHash.zip"
$Marker   = Join-Path $GameDir 'BepInEx\.ptf_bepinex_build'

if (-not (Test-Path (Join-Path $GameDir 'PassTheFear.exe'))) {
    throw "PassTheFear.exe not found in '$GameDir'. Pass -GameDir with the right path."
}

if ($Uninstall) {
    foreach ($rel in @('BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')) {
        $p = Join-Path $GameDir $rel
        if (Test-Path $p) { Remove-Item -Recurse -Force $p; Write-Host "  removed $rel" }
    }
    Write-Host "`nUninstalled. The game is back to stock - verify files in Steam if you want to be sure."
    return
}

# --- 1. BepInEx --------------------------------------------------------------------
$haveBuild = if (Test-Path $Marker) { (Get-Content $Marker -Raw).Trim() } else { '' }
if ($haveBuild -ne "$BepBuild") {
    Write-Host "[1/3] Installing BepInEx 6.0.0-be.$BepBuild ..."
    if (-not (Test-Path $BepZip)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $BepZip) | Out-Null
        Write-Host "  downloading (not cached yet)..."
        Invoke-WebRequest -Uri $BepUrl -OutFile $BepZip -UseBasicParsing
    }
    # Interop is generated against the old build; stale caches break the next launch.
    foreach ($rel in @('BepInEx\interop', 'BepInEx\cache', 'BepInEx\unity-libs')) {
        $p = Join-Path $GameDir $rel
        if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    }
    Expand-Archive -Path $BepZip -DestinationPath $GameDir -Force
    Set-Content -Path $Marker -Value "$BepBuild" -Encoding ASCII
} else {
    Write-Host "[1/3] BepInEx be.$BepBuild already installed."
}

# UnityLogListening pipes every Unity log line through BepInEx, which is slow and floods
# the file. Off by default; flip it to true when you need the game's own load progress.
$cfgDir = Join-Path $GameDir 'BepInEx\config'
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
@"
[Logging.Console]

Enabled = true

[Logging]

UnityLogListening = false
"@ | Set-Content -Path (Join-Path $cfgDir 'BepInEx.cfg') -Encoding UTF8

# --- 2. build ----------------------------------------------------------------------
$Dll = Join-Path $PSScriptRoot "$PluginName\bin\Release\net6.0\$PluginName.dll"
if (-not $SkipBuild) {
    Write-Host "[2/3] Building the plugin ..."
    # The csproj references the interop assemblies, so they must exist: that means the
    # game has to have been launched at least once since BepInEx was installed.
    if (-not (Test-Path (Join-Path $GameDir 'BepInEx\interop'))) {
        Write-Host ""
        Write-Host "  BepInEx\interop does not exist yet." -ForegroundColor Yellow
        Write-Host "  Launch the game once (it will take a minute to generate them), quit, then re-run this script."
        return
    }
    dotnet build $Project -c Release --nologo -p:GameDir="$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
} else {
    Write-Host "[2/3] Skipping build."
}
if (-not (Test-Path $Dll)) { throw "No plugin DLL at $Dll" }

# --- 3. install --------------------------------------------------------------------
Write-Host "[3/3] Installing the plugin ..."
New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
Copy-Item $Dll -Destination $PluginDir -Force
Copy-Item (Join-Path $PSScriptRoot "$PluginName\Thai.tsv") -Destination $PluginDir -Force

Write-Host ""
Write-Host "=== DONE ==="
Write-Host "Plugin: $PluginDir"
Write-Host "Log:    $GameDir\BepInEx\LogOutput.log"
Write-Host "Edit Thai.tsv in the plugin folder to change translations."
