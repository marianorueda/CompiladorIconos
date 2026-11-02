# Regenera Scanner.cs y Parser.cs desde IconScript.ATG (alternativa manual al build).
# Uso: .\scripts\gen.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root/tools/CocoR/CocoR.csproj" -nologo -v q
if (-not $?) { exit 1 }
Push-Location "$root/src/IconScript.Core"
try {
    dotnet "$root/tools/CocoR/bin/Debug/net8.0/Coco.dll" IconScript.ATG -namespace IconScript -frames "$root/tools/coco"
    $code = $LASTEXITCODE
    Remove-Item Scanner.cs.old, Parser.cs.old -ErrorAction SilentlyContinue
    exit $code
} finally { Pop-Location }
