[CmdletBinding()]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$version = '8.1.2-31-g8c9502e9b0'
$archiveName = 'ffmpeg-n8.1.2-31-g8c9502e9b0-win64-lgpl-8.1.zip'
$url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-07-26-13-28/' + $archiveName
$expectedHash = '923522df4e21c84cf6bd533ad690ea9b134087b38a95535a35abd786c25445c9'
$repoRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $repoRoot 'artifacts\dependencies\ffmpeg'
$archivePath = Join-Path $cacheRoot $archiveName
$extractRoot = Join-Path $cacheRoot $version
$binRoot = Join-Path $extractRoot 'ffmpeg-n8.1.2-31-g8c9502e9b0-win64-lgpl-8.1\bin'

New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
if ($Force -or -not (Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing
}

$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) {
    throw "FFmpeg archive SHA-256 mismatch. Expected $expectedHash, got $actualHash."
}

if ($Force -and (Test-Path -LiteralPath $extractRoot)) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}
if (-not (Test-Path -LiteralPath $binRoot)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot
}

Write-Output $binRoot
