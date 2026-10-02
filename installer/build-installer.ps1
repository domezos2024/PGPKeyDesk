<#
.SYNOPSIS
  Builds the PGPKeyDesk Windows installer (Inno Setup 6).
.DESCRIPTION
  Parses <Version> from PGPKeyDesk.vbproj, publishes self-contained win-x64 (unless -SkipPublish),
  compiles installer\PGPKeyDesk.iss and writes installer\Output\PGPKeyDesk-<Version>-win-x64-setup.exe + .sha256.
.EXAMPLE
  .\installer\build-installer.ps1
  .\installer\build-installer.ps1 -SkipPublish -PublishDir publish
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [string]$PublishDir = 'publish',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo 'PGPKeyDesk.vbproj'
[xml]$xml = Get-Content -LiteralPath $proj
$version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <Version> found in $proj" }
$version = $version.Trim()
Write-Host "Version: $version"

$publishPath = if ([System.IO.Path]::IsPathRooted($PublishDir)) { $PublishDir } else { Join-Path $repo $PublishDir }

if (-not $SkipPublish) {
    Push-Location $repo
    try {
        dotnet publish $proj -c $Configuration -r win-x64 --self-contained true -o $publishPath
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
    } finally { Pop-Location }
}
if (-not (Test-Path (Join-Path $publishPath 'PGPKeyDesk.exe'))) {
    throw "PGPKeyDesk.exe not found in $publishPath"
}

# Locate ISCC.exe: PATH, default install dirs, registry-less fallbacks.
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $iscc) {
    throw "ISCC.exe (Inno Setup 6) not found. Install it, e.g.: choco install innosetup -y   (or winget install JRSoftware.InnoSetup)"
}
Write-Host "ISCC: $iscc"

$script = Join-Path $PSScriptRoot 'PGPKeyDesk.iss'
# Pass the publish dir relative to the .iss when possible; absolute paths work as well.
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishPath" $script
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$exe = Join-Path $PSScriptRoot "Output\PGPKeyDesk-$version-win-x64-setup.exe"
if (-not (Test-Path $exe)) { throw "Expected installer not found: $exe" }

$name = Split-Path -Leaf $exe
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$exe.sha256", "$hash  $name`n", (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Installer: $exe"
Write-Host "SHA-256:   $hash"
