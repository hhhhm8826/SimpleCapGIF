[CmdletBinding()]
param([string]$BinRoot)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($BinRoot)) {
    $BinRoot = & (Join-Path $PSScriptRoot 'fetch-ffmpeg.ps1')
}

$ffmpeg = Join-Path $BinRoot 'ffmpeg.exe'
$ffprobe = Join-Path $BinRoot 'ffprobe.exe'
$expectedFfmpeg = 'e674aa31bc9e6f56f955c7ce87a194a5f949f67545b59f723f155053acb1269d'
$expectedFfprobe = 'ac9bf61f6f6f642e7f655e86ff60c7fe5670eebd16c18de3a9bbf81af03c50db'

foreach ($item in @(@($ffmpeg, $expectedFfmpeg), @($ffprobe, $expectedFfprobe))) {
    if (-not (Test-Path -LiteralPath $item[0])) { throw "Missing FFmpeg component: $($item[0])" }
    $hash = Get-Sha256 $item[0]
    if ($hash -ne $item[1]) { throw "Checksum mismatch for $($item[0])" }
}

$version = (& $ffmpeg -hide_banner -version 2>&1 | Out-String)
if ($version -match '--enable-gpl|--enable-nonfree|--enable-libx264|--enable-libx265') { throw 'Forbidden FFmpeg build flags were found.' }
if ($version -notmatch '--enable-libwebp') { throw 'FFmpeg libwebp support is missing.' }
$encoders = (& $ffmpeg -hide_banner -encoders 2>&1 | Out-String)
$filters = (& $ffmpeg -hide_banner -filters 2>&1 | Out-String)
foreach ($required in @('ffv1', 'gif', 'libwebp_anim')) { if ($encoders -notmatch $required) { throw "Missing encoder: $required" } }
foreach ($required in @('palettegen', 'paletteuse')) { if ($filters -notmatch $required) { throw "Missing filter: $required" } }

Write-Output "Verified FFmpeg at $BinRoot"
