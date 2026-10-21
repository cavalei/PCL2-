$ErrorActionPreference='Stop'
foreach($path in @('Resources/pcl-2.99.json','Resources/versions.json','src/PatcherCore.cs','.github/workflows/build.yml')) {
 if(-not(Test-Path -LiteralPath $path)){throw "Missing source: $path"}
}
$payload=Get-Content Resources/pcl-2.99.json -Raw | ConvertFrom-Json
if($payload.Methods.Count -ne 4){throw 'Incomplete method payload'}
Write-Output 'PASS: full source, four embedded method bodies and correct Actions directory'
