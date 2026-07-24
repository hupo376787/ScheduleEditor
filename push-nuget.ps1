[CmdletBinding()]
param(
    [string]$PackagePath,

    [string]$ApiKey = $env:NUGET_API_KEY,

    [ValidateNotNullOrEmpty()]
    [string]$Source = "https://api.nuget.org/v3/index.json",

    [bool]$SkipDuplicate = $true,

    [switch]$NoSymbols
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found."
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "No NuGet API key was provided. Set NUGET_API_KEY or pass -ApiKey."
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Get-ChildItem (Join-Path $PSScriptRoot "artifacts") -Filter "*.nupkg" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if ([string]::IsNullOrWhiteSpace($PackagePath) -or -not (Test-Path $PackagePath)) {
    throw "NuGet package not found. Run pack-release.ps1 first or pass -PackagePath."
}

$arguments = @(
    "nuget", "push", $PackagePath,
    "--api-key", $ApiKey,
    "--source", $Source
)

if ($SkipDuplicate) {
    $arguments += "--skip-duplicate"
}

if ($NoSymbols) {
    $arguments += "--no-symbols"
}

Write-Host "Publishing: $PackagePath"
Write-Host "Source    : $Source"

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet nuget push failed with exit code $LASTEXITCODE."
}

Write-Host "Package published successfully."
