param(
    [string]$Version = "1.0.0",
    [string]$Rid = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectPath = Join-Path $repoRoot "ProjectTimeTracker.csproj"

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "artifacts\usb-ready"
}

$publishDir = Join-Path $repoRoot ("artifacts\publish\" + $Rid)

Write-Host "Publishing single-file framework-dependent build..."
& dotnet publish $projectPath `
    -c $Configuration `
    -r $Rid `
    --self-contained false `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$sourceExe = Join-Path $publishDir "ProjectTimeTracker.exe"
if (!(Test-Path $sourceExe)) {
    throw "Expected output not found: $sourceExe"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$portableExeName = "ProjectTimeTracker_v${Version}_${Rid}_framework-dependent.exe"
$portableExePath = Join-Path $OutDir $portableExeName
Copy-Item -Path $sourceExe -Destination $portableExePath -Force

$hash = (Get-FileHash -Path $portableExePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = $portableExePath + ".sha256"
Set-Content -Path $hashPath -Value ("$hash *$portableExeName") -NoNewline

Write-Host "Done"
Write-Host "EXE:  $portableExePath"
Write-Host "SHA:  $hashPath"
