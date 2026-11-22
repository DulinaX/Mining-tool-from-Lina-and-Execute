# ENTWICKLUNGSHILFE: expliziter Download, niemals von der Anwendung aufgerufen.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$target = Join-Path $projectRoot 'work/toolchain'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$archive = Join-Path $target 'dotnet-sdk.zip'
$uri = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.428/dotnet-sdk-8.0.428-win-x64.zip'
$expected = 'CCE1DC5CFB2C5B73DDD9D3658F6009DEB85BE3A58DBEC44A6DFE3910BB32B044AD2FC6BB28C77ADED70C73270A204D207FE790DCB82FFCA6C7B42C1335B51C21'
if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $uri -OutFile $archive }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expected) { throw 'SDK-Prüfsumme stimmt nicht überein.' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $target 'dotnet') -Force
Write-Host 'SDK 8.0.428 lokal bereitgestellt. Keine systemweite Installation.'
