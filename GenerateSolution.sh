#!/usr/bin/env sh
set -eu

AETHER_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
"$AETHER_ROOT/Setup.sh"
dotnet "$AETHER_ROOT/Intermediate/BuildTool/Aether.BuildTool.dll" generate --root "$AETHER_ROOT"

printf 'Generated Visual Studio solution: %s\n' "$AETHER_ROOT/Intermediate/ProjectFiles/Aether.sln"
