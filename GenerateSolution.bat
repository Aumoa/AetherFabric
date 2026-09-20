@ECHO OFF
SETLOCAL

SET "AETHER_ROOT=%~dp0"
CALL "%AETHER_ROOT%Setup.bat" || EXIT /B 1

dotnet "%AETHER_ROOT%Intermediate\BuildTool\Aether.BuildTool.dll" generate --root "%AETHER_ROOT%." || EXIT /B 1

IF /I "%~1"=="--no-open" EXIT /B 0
START "" "%AETHER_ROOT%Intermediate\ProjectFiles\Aether.sln"

ENDLOCAL
