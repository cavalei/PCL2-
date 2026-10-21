param([string]$InputExe,[switch]$NoRestore)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 if(-not $NoRestore) {
  & dotnet restore tests/PclFix.Tests.csproj -p:NuGetAudit=false
  if($LASTEXITCODE -ne 0){throw 'Test restore failed'}
 }
 & dotnet build tests/PclFix.Tests.csproj --no-restore -c Release --verbosity quiet
 if($LASTEXITCODE -ne 0){throw 'Test build failed'}
 & ./tests/CheckSource.ps1
 if($InputExe){& ./tests/bin/Release/net48/PclFix.Tests.exe $InputExe}
 else {& ./tests/bin/Release/net48/PclFix.Tests.exe}
 if($LASTEXITCODE -ne 0){throw 'Regression tests failed'}
} finally {Pop-Location}
