$ErrorActionPreference='Stop'
# 数据自检：不需要 PCL 样本，CI 每次都能跑。
# 目的：防止载荷/签名/白名单被改坏而没人发现。

function Fail($msg) { throw "DATA CHECK FAILED: $msg" }
function Is-Hex64($s) { $s -is [string] -and $s -match '^[0-9A-Fa-f]{64}$' }

$w = Split-Path -Parent $PSScriptRoot
$payload  = Get-Content (Join-Path $w 'Resources/pcl-2.99.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$versions = Get-Content (Join-Path $w 'Resources/versions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sigs     = Get-Content (Join-Path $w 'Resources/signatures.json') -Raw -Encoding UTF8 | ConvertFrom-Json

# 1) 载荷完整性
if ($payload.Schema -ne 1) { Fail 'payload schema != 1' }
if ($payload.Methods.Count -ne 4) { Fail "payload method count = $($payload.Methods.Count), expected 4" }
$names = @{}
foreach ($m in $payload.Methods) {
    if (-not $m.Method) { Fail 'payload method without name' }
    if ($names.ContainsKey($m.Method)) { Fail "duplicate payload method: $($m.Method)" }
    $names[$m.Method] = 1
    if (-not (Is-Hex64 $m.CanonicalSha256)) { Fail "bad canonical hash for $($m.Method)" }
    if (-not $m.Instructions -or $m.Instructions.Count -lt 10) { Fail "suspiciously short body: $($m.Method)" }
    foreach ($i in $m.Instructions) {
        if (-not $i.Opcode) { Fail "instruction without opcode in $($m.Method)" }
        if (-not $i.Kind)   { Fail "instruction without operand kind in $($m.Method)" }
    }
}
foreach ($required in @(
    'System.Void PCL.PageDownloadInstall::LoaderInit()',
    'System.Void PCL.ModModpack::ModpackInstall()')) {
    if (-not $names.ContainsKey($required)) { Fail "required method missing: $required" }
}

# 2) 版本白名单
if ($versions.Count -lt 1) { Fail 'versions.json is empty' }
foreach ($v in $versions) {
    if (-not (Is-Hex64 $v.InputSha256))  { Fail "bad InputSha256: $($v.Version)" }
    if (-not (Is-Hex64 $v.OutputSha256)) { Fail "bad OutputSha256: $($v.Version)" }
    if ($v.InputSha256 -eq $v.OutputSha256) { Fail "input == output hash: $($v.Version)" }
}

# 3) 指纹签名
if ($sigs.Signatures.Count -lt 1) { Fail 'signatures.json has no entries' }
foreach ($s in $sigs.Signatures) {
    if (-not $s.Label) { Fail 'signature without label' }
    if ($s.MethodCount -le 0) { Fail "bad MethodCount: $($s.Label)" }
    if (-not (Is-Hex64 $s.FullRoot)) { Fail "bad FullRoot: $($s.Label)" }
    if (-not (Is-Hex64 $s.CoreRoot)) { Fail "bad CoreRoot: $($s.Label)" }
    if ($s.CoreCount -le 0 -or $s.CoreCount -ge $s.MethodCount) { Fail "CoreCount out of range: $($s.Label)" }
    if ($s.FullRoot -eq $s.CoreRoot) { Fail "FullRoot == CoreRoot: $($s.Label)" }
}

Write-Output ('PASS: payload=4 methods, versions=' + $versions.Count + ', signatures=' + $sigs.Signatures.Count + ', all hashes well-formed')
