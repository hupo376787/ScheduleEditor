[CmdletBinding()]
param(
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Get-ChildItem (Join-Path $PSScriptRoot "artifacts") -Filter "*.nupkg" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if ([string]::IsNullOrWhiteSpace($PackagePath) -or -not (Test-Path $PackagePath)) {
    throw "NuGet package not found."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $PackagePath))
try {
    $names = $archive.Entries.FullName
    $requiredPatterns = @(
        "*.nuspec",
        "lib/net10.0/ScheduleEditor.dll",
        "README.md",
        "README.zh-CN.md",
        "README.en-US.md",
        "LICENSE",
        "docs/language-pack.example.json",
        "docs/language-overrides.example.json"
    )

    $missing = @()
    foreach ($pattern in $requiredPatterns) {
        if (-not ($names | Where-Object { $_ -like $pattern })) {
            $missing += $pattern
        }
    }

    Write-Host "Package contents:"
    $names | Sort-Object | ForEach-Object { Write-Host "  $_" }

    if ($missing.Count -gt 0) {
        throw "Package verification failed. Missing: $($missing -join ', ')"
    }

    $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
    if ($null -eq $nuspecEntry) {
        throw "Package verification failed. The nuspec file is missing."
    }

    $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ($metadata.id -ne "ScheduleEditor") {
        throw "Unexpected package ID: $($metadata.id)"
    }
    if ($metadata.version -ne "1.0.0") {
        throw "Unexpected package version: $($metadata.version)"
    }
    if ($metadata.authors -ne "vincent, chatgpt") {
        throw "Unexpected package authors: $($metadata.authors)"
    }
    if ($metadata.repository.url -ne "https://github.com/hupo376787/ScheduleEditor") {
        throw "Unexpected repository URL: $($metadata.repository.url)"
    }

    Write-Host "Package ID      : $($metadata.id)"
    Write-Host "Package version : $($metadata.version)"
    Write-Host "Authors         : $($metadata.authors)"
    Write-Host "Repository      : $($metadata.repository.url)"
    Write-Host "Package verification passed."
}
finally {
    $archive.Dispose()
}
