[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$extensions = @(
    '.config', '.cs', '.csproj', '.gitattributes', '.gitignore', '.json', '.manifest',
    '.md', '.props', '.ps1', '.resx', '.sln', '.targets', '.xaml', '.xml', '.yaml', '.yml'
)
$specialNames = @('.editorconfig', '.gitattributes', '.gitignore', 'LICENSE')
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$failures = [System.Collections.Generic.List[string]]::new()

$paths = & git -C $repoRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate repository files.' }

foreach ($relativePath in $paths) {
    $extension = [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()
    $name = [System.IO.Path]::GetFileName($relativePath)
    if ($extensions -notcontains $extension -and $specialNames -notcontains $name) { continue }

    $path = Join-Path $repoRoot $relativePath
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $failures.Add("$relativePath uses a UTF-8 BOM.")
    }

    try {
        $content = $strictUtf8.GetString($bytes)
    }
    catch {
        $failures.Add("$relativePath is not valid UTF-8.")
        continue
    }

    if ($content -match "(?<!`r)`n" -or $content -match "`r(?!`n)") {
        $failures.Add("$relativePath does not use CRLF consistently.")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Text format verification failed for $($failures.Count) file(s)."
}

Write-Output 'Verified repository text files: UTF-8 without BOM, CRLF line endings.'
