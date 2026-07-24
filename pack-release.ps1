[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Version = "1.0.0",

    [ValidateNotNullOrEmpty()]
    [string]$PackageId = "ScheduleEditor",

    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root "src/ScheduleEditor/ScheduleEditor.csproj"
$solution = Join-Path $root "ScheduleEditor.sln"
$nugetConfig = Join-Path $root "NuGet.Config"
$artifacts = Join-Path $root "artifacts"
$packages = Join-Path $root ".nuget/packages"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the .NET 10 SDK and reopen the terminal."
}

if (-not (Test-Path $project)) {
    throw "Component project not found: $project"
}

if (-not $KeepArtifacts -and (Test-Path $artifacts)) {
    Remove-Item $artifacts -Recurse -Force
}

New-Item $artifacts -ItemType Directory -Force | Out-Null
New-Item $packages -ItemType Directory -Force | Out-Null

Write-Host "[1/4] Restoring packages..."
& dotnet restore $solution `
    --configfile $nugetConfig `
    --packages $packages `
    --force
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

Write-Host "[2/4] Building Release..."
& dotnet build $project `
    --configuration Release `
    --no-restore `
    -p:PackageId=$PackageId `
    -p:PackageVersion=$Version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

Write-Host "[3/4] Creating NuGet package..."
& dotnet pack $project `
    --configuration Release `
    --output $artifacts `
    --no-build `
    -p:PackageId=$PackageId `
    -p:PackageVersion=$Version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$nupkg = Get-ChildItem $artifacts -Filter "*.nupkg" |
    Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

$snupkg = Get-ChildItem $artifacts -Filter "*.snupkg" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $nupkg) {
    throw "Packing completed, but no .nupkg file was found in $artifacts."
}

Write-Host "[4/4] Package created successfully."
Write-Host "Package : $($nupkg.FullName)"
if ($null -ne $snupkg) {
    Write-Host "Symbols : $($snupkg.FullName)"
}
Write-Host ""
Write-Host "Before publishing, install the package from the artifacts folder in a clean test application."
