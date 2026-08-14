[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildPropsPath = Join-Path $repoRoot 'Directory.Build.props'
[xml]$buildProps = Get-Content -LiteralPath $buildPropsPath -Raw
$appVersion = [string]$buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($appVersion)) { throw 'The application version is missing from Directory.Build.props.' }
$packageName = "SimpleCapGIF-v$appVersion-win-x64"
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($dotnet) -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
if ([string]::IsNullOrWhiteSpace($dotnet)) { throw '.NET 10 SDK was not found.' }
$ffmpegBin = & (Join-Path $PSScriptRoot 'fetch-ffmpeg.ps1')
& (Join-Path $PSScriptRoot 'verify-ffmpeg.ps1') -BinRoot $ffmpegBin

$packageRoot = Join-Path $repoRoot 'artifacts\package'
$publishRoot = Join-Path $packageRoot $packageName
$publishStagingRoot = Join-Path $packageRoot "$packageName.publish"
$zipPath = Join-Path $packageRoot "$packageName.zip"
if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
if (Test-Path -LiteralPath $publishStagingRoot) { Remove-Item -LiteralPath $publishStagingRoot -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

try {
    & $dotnet publish (Join-Path $repoRoot 'src\SimpleCapGIF.App\SimpleCapGIF.App.csproj') `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $publishStagingRoot `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    $stagedExe = Join-Path $publishStagingRoot 'SimpleCapGIF.exe'
    if (-not (Test-Path -LiteralPath $stagedExe)) { throw 'Single-file SimpleCapGIF.exe is missing from publish output.' }
    $unexpectedPublishFiles = @(Get-ChildItem -LiteralPath $publishStagingRoot -Recurse -File | Where-Object { $_.FullName -ne $stagedExe })
    if ($unexpectedPublishFiles.Count -gt 0) {
        throw "Single-file publish produced unexpected loose files: $($unexpectedPublishFiles.FullName -join ', ')"
    }

    $publishedExe = Join-Path $publishRoot 'SimpleCapGIF.exe'
    Copy-Item -LiteralPath $stagedExe -Destination $publishedExe
    $versionInfo = (Get-Item -LiteralPath $publishedExe).VersionInfo
    if ($versionInfo.ProductVersion -ne $appVersion) { throw "Unexpected product version: $($versionInfo.ProductVersion)" }
    if ($versionInfo.FileVersion -ne "$appVersion.0") { throw "Unexpected file version: $($versionInfo.FileVersion)" }

    $resourcesRoot = Join-Path $publishRoot 'resources'
    $ffmpegDestination = Join-Path $resourcesRoot 'ffmpeg'
    New-Item -ItemType Directory -Force -Path $ffmpegDestination | Out-Null
    Copy-Item -LiteralPath (Join-Path $ffmpegBin 'ffmpeg.exe'), (Join-Path $ffmpegBin 'ffprobe.exe') -Destination $ffmpegDestination
    $ffmpegRoot = Split-Path -Parent $ffmpegBin
    Copy-Item -LiteralPath (Join-Path $ffmpegRoot 'LICENSE.txt') -Destination (Join-Path $ffmpegDestination 'LICENSE.txt')

    $packageDocs = Join-Path $resourcesRoot 'docs'
    New-Item -ItemType Directory -Force -Path $packageDocs | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'README.ko-KR.md'), (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $packageDocs
    $packageImages = Join-Path $packageDocs 'images'
    New-Item -ItemType Directory -Force -Path $packageImages | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'images\simplecapgif-logo.png'), (Join-Path $repoRoot 'images\simplecapgif-app.png'), (Join-Path $repoRoot 'images\simplecapgif-app.ko-KR.png') -Destination $packageImages

    foreach ($document in @('README.md', 'README.ko-KR.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDocs $document))) { throw "Package document is missing: $document" }
    }
    foreach ($image in @('simplecapgif-logo.png', 'simplecapgif-app.png', 'simplecapgif-app.ko-KR.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageImages $image))) { throw "README image is missing: $image" }
    }
    if (Test-Path -LiteralPath (Join-Path $packageDocs 'DEVELOPMENT.md')) { throw 'Developer documentation must not be included in the portable package.' }
    if (Test-Path -LiteralPath (Join-Path $packageDocs 'docs')) { throw 'The repository docs directory must not be included in the portable package.' }

    $expectedRootEntries = @('SimpleCapGIF.exe', 'resources')
    $unexpectedRootEntries = @(Get-ChildItem -LiteralPath $publishRoot -Force | Where-Object { $_.Name -notin $expectedRootEntries })
    if ($unexpectedRootEntries.Count -gt 0) {
        throw "Unexpected package root entries: $($unexpectedRootEntries.Name -join ', ')"
    }

    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $publishStagingRoot) { Remove-Item -LiteralPath $publishStagingRoot -Recurse -Force }
}

Write-Output $zipPath
