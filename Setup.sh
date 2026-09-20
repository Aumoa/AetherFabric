#!/usr/bin/env sh
set -eu

AETHER_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
AETHER_BUILD_TOOL_PROJECT="$AETHER_ROOT/tools/Aether.BuildTool/Aether.BuildTool.csproj"
AETHER_BUILD_TOOL_OUTPUT="$AETHER_ROOT/Intermediate/BuildTool"

dotnet restore "$AETHER_BUILD_TOOL_PROJECT" --configfile "$AETHER_ROOT/NuGet.Config"
dotnet publish "$AETHER_BUILD_TOOL_PROJECT" -c Release -o "$AETHER_BUILD_TOOL_OUTPUT" --no-restore
