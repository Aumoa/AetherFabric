@ECHO OFF
SETLOCAL

SET "AETHER_ROOT=%~dp0"
SET "AETHER_BUILD_TOOL_PROJECT=%AETHER_ROOT%tools\Aether.BuildTool\Aether.BuildTool.csproj"
SET "AETHER_BUILD_TOOL_OUTPUT=%AETHER_ROOT%Intermediate\BuildTool"

dotnet restore "%AETHER_BUILD_TOOL_PROJECT%" --configfile "%AETHER_ROOT%NuGet.Config" || EXIT /B 1
dotnet publish "%AETHER_BUILD_TOOL_PROJECT%" -c Release -o "%AETHER_BUILD_TOOL_OUTPUT%" --no-restore || EXIT /B 1

ENDLOCAL
