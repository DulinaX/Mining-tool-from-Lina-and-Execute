# Baut "Exi und Lina Mining Tool" und legt die fertige App auf den Desktop.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Write-Host ''
Write-Host '  ===  Exi und Lina Mining Tool  -  App bauen  ===' -ForegroundColor Magenta
Write-Host ''

# 1) Bau-Werkzeug (.NET SDK 8.0.428) suchen
$localSdk = Join-Path $root 'work\toolchain\dotnet\dotnet.exe'
$sdk = $null
if (Test-Path -LiteralPath $localSdk) { $sdk = $localSdk }
else {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) {
        $list = & $cmd.Source --list-sdks 2>$null
        if ($list -match '^8\.0\.428 ') { $sdk = $cmd.Source }
    }
}
if (-not $sdk) {
    Write-Host 'Das Bau-Werkzeug (.NET SDK 8.0.428, ca. 220 MB) fehlt noch.' -ForegroundColor Yellow
    Write-Host 'Es wird jetzt EINMALIG direkt von Microsoft in den Ordner "work\toolchain" geladen.'
    Write-Host 'Es wird nichts auf dem PC installiert. Das kann ein paar Minuten dauern ...'
    & (Join-Path $PSScriptRoot 'bootstrap-sdk.ps1')
    $sdk = $localSdk
}

# 2) App bauen
Write-Host ''
Write-Host 'Baue die App ... (1-3 Minuten)' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'build.ps1') -SdkPath $sdk -SkipTests

# 3) Fertige App auf den Desktop kopieren (Einstellungen bleiben erhalten, Erz-Liste wird aktualisiert)
$out = Join-Path $root 'outputs\ExiUndLinaMiningTool-win-x64'
$target = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Exi und Lina Mining Tool'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$keep = @{}
foreach ($rel in @('settings.json')) {
    $p = Join-Path $target $rel
    if (Test-Path -LiteralPath $p) { $keep[$rel] = [IO.File]::ReadAllBytes($p) }
}
Copy-Item -Path (Join-Path $out '*') -Destination $target -Recurse -Force
foreach ($rel in $keep.Keys) { [IO.File]::WriteAllBytes((Join-Path $target $rel), $keep[$rel]) }
Copy-Item -LiteralPath (Join-Path $root 'SO GEHTS.txt') -Destination $target -Force

Write-Host ''
Write-Host 'FERTIG!' -ForegroundColor Green
Write-Host "Die App liegt jetzt hier:  $target"
Write-Host 'Starten mit Doppelklick auf  ExiUndLinaMiningTool.exe'
Start-Process explorer.exe -ArgumentList "`"$target`""
