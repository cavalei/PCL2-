param([string]$PackageSource,[string]$PackageCache,[switch]$NoRestore)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 if(-not $NoRestore) {
  $restore=@('restore','PclModpackEnabler.csproj','-p:NuGetAudit=false')
  if($PackageSource){$restore+=@('--source',$PackageSource)}
  if($PackageCache){$restore+=@('--packages',$PackageCache)}
  & dotnet @restore
  if($LASTEXITCODE -ne 0){throw 'Restore failed'}
 }
 & dotnet build PclModpackEnabler.csproj --no-restore -c Release --verbosity quiet
 if($LASTEXITCODE -ne 0){throw 'Build failed'}
 New-Item -ItemType Directory -Force dist | Out-Null
 Copy-Item -LiteralPath bin/Release/net48/PclModpackEnabler.exe -Destination dist/PclModpackEnabler.exe -Force
 $hash=(Get-FileHash -LiteralPath dist/PclModpackEnabler.exe -Algorithm SHA256).Hash
 Write-Output "Built dist/PclModpackEnabler.exe SHA256=$hash"
} finally {Pop-Location}
