# Baut die Version zum Weitergeben: "Mining Tool" (© 2026 DulinaX & Execute)
# Ergebnis: Ordner "Mining Tool (Weitergabe)" und "Mining Tool.zip" auf dem Desktop - ohne eigene Einstellungen.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Write-Host ''
Write-Host '  ===  Mining Tool  -  Version zum Weitergeben bauen  ===' -ForegroundColor Magenta
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
    Write-Host 'Das Bau-Werkzeug (.NET SDK 8.0.428) fehlt noch und wird EINMALIG von Microsoft geladen ...' -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'bootstrap-sdk.ps1')
    $sdk = $localSdk
}

Push-Location $root
$previous = @{}
foreach ($key in @('DOTNET_CLI_HOME','APPDATA','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_NOLOGO','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')) { $previous[$key] = [Environment]::GetEnvironmentVariable($key) }
try {
    $env:DOTNET_CLI_HOME = Join-Path $root 'work/dotnet-home'
    $env:APPDATA = Join-Path $root 'work/build-appdata'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    & (Join-Path $PSScriptRoot 'verify-resources.ps1')
    function Invoke-Dotnet([string[]]$Arguments) {
        & $sdk @Arguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet fehlgeschlagen (Exit $LASTEXITCODE): $Arguments" }
    }

    # 2) App bauen (eigene Zwischenordner, damit sich die beiden Versionen nicht in die Quere kommen)
    Write-Host 'Baue die App ... (1-3 Minuten)' -ForegroundColor Cyan
    $app = 'src/MiningScanner.Windows/MiningScanner.Windows.csproj'
    $out = Join-Path $root 'outputs\MiningTool-win-x64'
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    $share = @('-p:ShareBuild=true', '-p:IntermediateOutputPath=obj\share\', '-p:OutputPath=bin\share\')
    Invoke-Dotnet (@('restore', $app, '-r', 'win-x64', '-p:SelfContained=true', '--configfile', 'NuGet.Config') + $share)
    Invoke-Dotnet (@('publish', $app, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $out, '--no-restore', '-p:UseSharedCompilation=false', '-m:1', '-nr:false') + $share)

    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $out -Force
    Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $out -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root 'LIES MICH - Mining Tool.txt') -Destination (Join-Path $out 'README - LIES MICH.txt') -Force
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'resources/native') -Filter '*.dll' -File -ErrorAction SilentlyContinue) {
        Copy-Item -LiteralPath $file.FullName -Destination $out -Force
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $out 'x64') -Force
    }
    # Keine persönlichen Dateien mitgeben
    foreach ($personal in @('settings.json', 'language.txt', 'fehler.log')) {
        $p = Join-Path $out $personal
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
    }
    foreach ($required in @('MiningTool.exe', 'tessdata/eng.traineddata', 'data/signatures.json', 'x64/tesseract50.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $out $required))) { throw "Unvollständige Ausgabe: $required fehlt" }
    }

    # 3) Auf den Desktop: Ordner + ZIP zum Verschicken
    $desktop = [Environment]::GetFolderPath('Desktop')
    $folder = Join-Path $desktop 'Mining Tool (Weitergabe)'
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    Copy-Item -Path (Join-Path $out '*') -Destination $folder -Recurse -Force
    $zip = Join-Path $desktop 'Mining Tool.zip'
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal

    Write-Host ''
    Write-Host 'FERTIG!' -ForegroundColor Green
    Write-Host "Zum Verschicken:   $zip"
    Write-Host "Zum Testen:        $folder\MiningTool.exe"
    Start-Process explorer.exe -ArgumentList "/select,`"$zip`""
}
finally {
    if ($sdk -and (Test-Path -LiteralPath $sdk)) { & $sdk build-server shutdown | Out-Null }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key]) }
    Pop-Location
}
