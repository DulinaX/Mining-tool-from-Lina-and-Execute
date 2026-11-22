param(
    [string]$SdkPath = '',
    [switch]$SkipTests,
    [switch]$FrameworkDependent
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
$previous = @{}
foreach ($key in @('DOTNET_CLI_HOME','APPDATA','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_NOLOGO','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')) { $previous[$key] = [Environment]::GetEnvironmentVariable($key) }
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot 'work/dotnet-home'
    $env:APPDATA = Join-Path $projectRoot 'work/build-appdata'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    if (!$SdkPath) {
        $localSdk = Join-Path $projectRoot 'work/toolchain/dotnet/dotnet.exe'
        if (Test-Path -LiteralPath $localSdk) { $SdkPath = $localSdk } else { $SdkPath = (Get-Command dotnet -ErrorAction Stop).Source }
    }
    if ((& $SdkPath --version) -ne '8.0.428') { throw 'Erforderlich: .NET SDK 8.0.428. Siehe scripts/bootstrap-sdk.ps1.' }
    & (Join-Path $PSScriptRoot 'verify-resources.ps1')
    function Invoke-Dotnet([string[]]$Arguments) {
        & $SdkPath @Arguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet fehlgeschlagen (Exit $LASTEXITCODE): $Arguments" }
    }
    $app = 'src/MiningScanner.Windows/MiningScanner.Windows.csproj'
    Invoke-Dotnet @('restore', $app, '--configfile', 'NuGet.Config')
    Invoke-Dotnet @('build', $app, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '-m:1', '-nr:false')
    if (!$SkipTests) {
        foreach ($test in @('Core','Ocr','Integration')) {
            $path = "tests/MiningScanner.$test.Tests/MiningScanner.$test.Tests.csproj"
            Invoke-Dotnet @('restore', $path, '--configfile', 'NuGet.Config')
            Invoke-Dotnet @('build', $path, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '-m:1', '-nr:false')
            if ($test -eq 'Core') { Invoke-Dotnet @('run','--project',$path,'-c','Release','--no-build','--','data/signatures.json') }
            elseif ($test -eq 'Ocr') { Invoke-Dotnet @('run','--project',$path,'-c','Release','--no-build','--','resources/tessdata') }
            else { Invoke-Dotnet @('run','--project',$path,'-c','Release','--no-build') }
        }
    }
    $output = Join-Path $projectRoot 'outputs/MiningTool-win-x64'
    if ((Test-Path -LiteralPath $output) -and !(Test-Path -LiteralPath (Join-Path $output '.mining-scanner-output'))) {
        throw "Ausgabeordner existiert ohne Projektmarkierung. Er wird nicht überschrieben: $output"
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $output '.mining-scanner-output') -Value 'Mining Tool build output' -Encoding utf8
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    Invoke-Dotnet @('restore',$app,'-r','win-x64',"-p:SelfContained=$selfContained",'--configfile','NuGet.Config')
    Invoke-Dotnet @('publish',$app,'-c','Release','-r','win-x64','--self-contained',$selfContained,'-o',$output,'--no-restore','-p:UseSharedCompilation=false','-m:1','-nr:false')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $output -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $output -Recurse -Force
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'resources/native') -Filter '*.dll' -File -ErrorAction SilentlyContinue) {
        Copy-Item -LiteralPath $file.FullName -Destination $output -Force
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $output 'x64') -Force
    }
    foreach ($required in @('MiningTool.exe','tessdata/eng.traineddata','data/signatures.json','x64/tesseract50.dll','x64/leptonica-1.82.0.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $output $required))) { throw "Unvollständige Ausgabe: $required fehlt" }
    }
    Write-Host 'Datenschutz-Quellcodepruefung: siehe docs/PRIVACY-AUDIT.md.'
    Write-Host "Fertig: $output/MiningTool.exe"
}
finally {
    if ($SdkPath -and (Test-Path -LiteralPath $SdkPath)) { & $SdkPath build-server shutdown | Out-Null }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key]) }
    Pop-Location
}
