[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($dotnet) -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
if ([string]::IsNullOrWhiteSpace($dotnet)) { throw '.NET 10 SDK was not found.' }
$ffmpegBin = & (Join-Path $PSScriptRoot 'fetch-ffmpeg.ps1')
& (Join-Path $PSScriptRoot 'verify-ffmpeg.ps1') -BinRoot $ffmpegBin

$packageRoot = Join-Path $repoRoot 'artifacts\package'
$publishRoot = Join-Path $packageRoot 'SimpleCapGIF-v0.1-win-x64'
$zipPath = Join-Path $packageRoot 'SimpleCapGIF-v0.1-win-x64.zip'
if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

& $dotnet publish (Join-Path $repoRoot 'src\SimpleCapGIF.App\SimpleCapGIF.App.csproj') --configuration $Configuration --runtime win-x64 --self-contained true --output $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$ffmpegDestination = Join-Path $publishRoot 'ffmpeg'
New-Item -ItemType Directory -Force -Path $ffmpegDestination | Out-Null
Copy-Item -LiteralPath (Join-Path $ffmpegBin 'ffmpeg.exe'), (Join-Path $ffmpegBin 'ffprobe.exe') -Destination $ffmpegDestination
$ffmpegRoot = Split-Path -Parent $ffmpegBin
Copy-Item -LiteralPath (Join-Path $ffmpegRoot 'LICENSE.txt') -Destination (Join-Path $ffmpegDestination 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'README.ko-KR.md'), (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $publishRoot
$packageImages = Join-Path $publishRoot 'images'
New-Item -ItemType Directory -Force -Path $packageImages | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'images\simplecapgif-logo.png'), (Join-Path $repoRoot 'images\simplecapgif-app.png'), (Join-Path $repoRoot 'images\simplecapgif-app.ko-KR.png') -Destination $packageImages
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'SimpleCapGIF.exe'))) { throw 'SimpleCapGIF.exe is missing from publish output.' }
$localizedCultures = @('ko-KR', 'ja-JP', 'zh-Hans', 'pt-BR', 'es', 'de-DE', 'fr-FR', 'zh-Hant')
foreach ($culture in $localizedCultures) {
    $resourcePath = Join-Path $publishRoot "$culture\SimpleCapGIF.Localization.resources.dll"
    if (-not (Test-Path -LiteralPath $resourcePath)) { throw "Localization resource is missing: $culture" }
}
foreach ($document in @('README.md', 'README.ko-KR.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $document))) { throw "Package document is missing: $document" }
}
foreach ($image in @('simplecapgif-logo.png', 'simplecapgif-app.png', 'simplecapgif-app.ko-KR.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot "images\$image"))) { throw "README image is missing: $image" }
}
if (Test-Path -LiteralPath (Join-Path $publishRoot 'DEVELOPMENT.md')) { throw 'Developer documentation must not be included in the portable package.' }
if (Test-Path -LiteralPath (Join-Path $publishRoot 'docs')) { throw 'The docs directory must not be included in the portable package.' }
Write-Output $zipPath
