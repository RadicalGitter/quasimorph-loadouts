[CmdletBinding()]
param(
    [string]$GamePath = $env:QUASIMORPH_GAME_PATH,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $knownPath = "X:\SteamLibrary\steamapps\common\Quasimorph"
    if (Test-Path -LiteralPath $knownPath) {
        $GamePath = $knownPath
    }
}

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    throw "Pass -GamePath or set QUASIMORPH_GAME_PATH."
}

$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$managedPath = Join-Path $GamePath "Quasimorph_Data\Managed"
$requiredReferences = @(
    "Assembly-CSharp.dll",
    "0Harmony.dll",
    "Newtonsoft.Json.dll",
    "netstandard.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.InputLegacyModule.dll"
)

foreach ($reference in $requiredReferences) {
    $referencePath = Join-Path $managedPath $reference
    if (-not (Test-Path -LiteralPath $referencePath)) {
        throw "Missing game build input: $referencePath"
    }
}

$artifactRoot = Join-Path $projectRoot "artifacts"
$packageRoot = Join-Path $artifactRoot "QuasimorphLoadouts"
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
$outputDll = Join-Path $packageRoot "QuasimorphLoadouts.dll"

$sdkLines = & dotnet --list-sdks 2>$null
if ($LASTEXITCODE -eq 0 -and $sdkLines) {
    & dotnet build (Join-Path $projectRoot "src\QuasimorphLoadouts.csproj") `
        --configuration $Configuration `
        --property:GamePath="$GamePath" `
        --output $packageRoot
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}
else {
    $compiler = "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
    if (-not (Test-Path -LiteralPath $compiler)) {
        throw "No .NET SDK or compatible C# compiler was found. Install the .NET 8 SDK and retry."
    }

    $arguments = @(
        "/nologo",
        "/target:library",
        "/langversion:latest",
        "/optimize+",
        "/debug:portable",
        "/out:$outputDll"
    )
    foreach ($reference in $requiredReferences) {
        $arguments += "/reference:" + (Join-Path $managedPath $reference)
    }
    $arguments += Get-ChildItem -LiteralPath (Join-Path $projectRoot "src") -Filter "*.cs" -File -Recurse |
        Select-Object -ExpandProperty FullName

    & $compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "C# compilation failed."
    }
}

Copy-Item -LiteralPath (Join-Path $projectRoot "modmanifest.json") -Destination $packageRoot -Force

if ($Install) {
    $localLow = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $localLow = [System.IO.Path]::GetFullPath((Join-Path $localLow "..\LocalLow"))
    $installRoot = Join-Path $localLow "Magnum Scriptum Ltd\Quasimorph\LocalUserPresets\QuasimorphLoadouts"
    New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
    Copy-Item -LiteralPath $outputDll -Destination $installRoot -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot "modmanifest.json") -Destination $installRoot -Force
    Write-Host "Installed to $installRoot"
}

Write-Host "Built package: $packageRoot"
