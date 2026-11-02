#!/usr/bin/env sh
# Regenera Scanner.cs y Parser.cs desde IconScript.ATG (alternativa manual al build).
# Uso: ./scripts/gen.sh
set -e
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet build "$root/tools/CocoR/CocoR.csproj" -nologo -v q
cd "$root/src/IconScript.Core"
dotnet "$root/tools/CocoR/bin/Debug/net8.0/Coco.dll" IconScript.ATG -namespace IconScript -frames "$root/tools/coco"
rm -f Scanner.cs.old Parser.cs.old
