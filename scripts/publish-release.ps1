# Builds a ready-to-run Windows zip for GitHub Releases.
# Usage:
#   .\scripts\publish-release.ps1
#   .\scripts\publish-release.ps1 -Version 1.0.0

[CmdletBinding()]
param(
    [string]$Version = "",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "NpcNotebook\NpcNotebook.csproj"

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$csproj = Get-Content $project
    $Version = $csproj.Project.PropertyGroup.Version |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Specify -Version or set <Version> in the .csproj."
    }
}

$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$stageDir = Join-Path $repoRoot "artifacts\stage\NpcNotebook"
$zipPath = Join-Path $repoRoot "artifacts\NpcNotebook-v$Version-$Runtime.zip"

Write-Host "Publishing NpcNotebook $Version ($Runtime, self-contained)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $stageDir -Parent) -Force | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item $publishDir $stageDir -Recurse -Force

$readmePath = Join-Path $stageDir "README.txt"
@"
NpcNotebook v$Version
======================

Как запустить
-------------
1. Распакуйте архив в любую папку.
2. Запустите NpcNotebook.exe.
3. Установка .NET не требуется (сборка self-contained).

Системные требования
--------------------
- Windows 10 / 11 (x64)

Сохранения
----------
Блокноты хранятся в файлах .npcbook (Открыть / Сохранить в программе).

Поддержка
---------
dndtools.lebedev@proton.me
"@ | Set-Content -Path $readmePath -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $stageDir -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ""
Write-Host "Ready:"
Write-Host "  $zipPath"
Write-Host ""
Write-Host "Next: upload this zip to a GitHub Release (tag v$Version)."
