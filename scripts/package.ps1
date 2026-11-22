$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $projectRoot 'outputs'
$portable = Join-Path $outputRoot 'ExecutesMiningScanner-1.0.1-win-x64'
if (!(Test-Path -LiteralPath (Join-Path $portable 'ExecutesMiningScanner.exe'))) { throw 'Bitte zuerst scripts/build.ps1 erfolgreich ausfuehren.' }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ProjectZip([string]$destination, [string]$baseDirectory, [string[]]$files, [string]$prefix) {
    # Every file is explicitly selected from this project's own folders.
    $stream = [IO.File]::Open($destination, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($path in ($files | Sort-Object -Unique)) {
                $fullPath = [IO.Path]::GetFullPath($path)
                if (!$fullPath.StartsWith($baseDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Datei liegt ausserhalb des Projektbereichs.' }
                $relative = $fullPath.Substring($baseDirectory.Length + 1).Replace('\','/')
                $entry = $zip.CreateEntry($prefix + $relative, [IO.Compression.CompressionLevel]::Optimal)
                # Reproducible archive timestamps. Contents are verified independently.
                $entry.LastWriteTime = [DateTimeOffset]::new(2026,11,16,0,0,0,[TimeSpan]::Zero)
                $input = [IO.File]::OpenRead($fullPath)
                try { $entryStream = $entry.Open(); try { $input.CopyTo($entryStream) } finally { $entryStream.Dispose() } }
                finally { $input.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
}

$portableFiles = @(Get-ChildItem -LiteralPath $portable -Recurse -File | Where-Object { $_.Name -notin @('settings.json','.mining-scanner-output','createdump.exe') } | ForEach-Object FullName)
$portableZip = Join-Path $outputRoot 'ExecutesMiningScanner-1.0.1-Windows-x64.zip'
New-ProjectZip $portableZip $portable $portableFiles 'ExecutesMiningScanner/'

$sourceFiles = @()
foreach ($directory in @('src','docs','licenses','data','resources/tessdata','tests/MiningScanner.Core.Tests','tests/MiningScanner.Ocr.Tests','tests/MiningScanner.Integration.Tests')) {
    $sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot $directory) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object FullName)
}
foreach ($file in @('README.md','LICENSE','global.json','NuGet.Config','Directory.Build.props','resources-manifest.json','.gitignore','scripts/build.ps1','scripts/bootstrap-sdk.ps1','scripts/verify-resources.ps1','scripts/package.ps1')) {
    $sourceFiles += Join-Path $projectRoot $file
}
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'resources-manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
foreach ($entry in $manifest.files) { $sourceFiles += Join-Path $projectRoot $entry.path }
$sourceZip = Join-Path $outputRoot 'ExecutesMiningScanner-1.0.1-Source.zip'
New-ProjectZip $sourceZip $projectRoot $sourceFiles 'ExecutesMiningScanner-Source/'

foreach ($archive in @($portableZip,$sourceZip)) {
    $check = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        if ($check.Entries.Count -eq 0) { throw 'Leeres Paket.' }
        if ($check.Entries.FullName -match '(?i)(CaptureHarness|StarCitizen\.exe|audit-source\.ps1|settings\.json$|createdump\.exe$)') { throw 'Unerwartete Test-/Benutzerdatei im Paket.' }
        Write-Host "$([IO.Path]::GetFileName($archive)): $($check.Entries.Count) Eintraege, $((Get-Item -LiteralPath $archive).Length) Bytes"
    } finally { $check.Dispose() }
}
Get-FileHash -LiteralPath $portableZip,$sourceZip -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'package-sha256.json') -Encoding utf8
