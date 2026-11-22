$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $projectRoot 'resources-manifest.json'
if (!(Test-Path -LiteralPath $manifestPath)) { throw 'resources-manifest.json fehlt.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$hasVendor = Test-Path -LiteralPath (Join-Path $projectRoot 'vendor/nuget')
foreach ($entry in $manifest.files) {
    # GitHub-Version: Ohne vendor-Ordner werden die NuGet-Pakete von nuget.org geladen.
    if (!$hasVendor -and $entry.path -like 'vendor/*') { continue }
    $candidate = [IO.Path]::GetFullPath((Join-Path $projectRoot $entry.path))
    if (!$candidate.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Ungültiger Ressourcenpfad.' }
    if (!(Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Ressource fehlt: $($entry.path)" }
    if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Prüfsummenfehler: $($entry.path)" }
}
Write-Host "$($manifest.files.Count) lokale Ressourcen geprüft. Kein Download erforderlich."
