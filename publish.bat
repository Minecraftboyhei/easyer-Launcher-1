@echo off
rem Rebuild the single-file release package into dist\win-x64
rem Run this after ANY code change, otherwise the exe users double-click stays old.
cd /d "%~dp0"

taskkill /F /IM EasyerLauncher.exe >nul 2>&1

dotnet publish EasyerLauncher\EasyerLauncher.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None -p:DebugSymbols=false ^
  -o dist\win-x64

if errorlevel 1 (
  echo.
  echo [FAIL] publish failed
  pause
  exit /b 1
)

echo.
echo [OK] dist\win-x64\EasyerLauncher.exe
for %%F in ("dist\win-x64\EasyerLauncher.exe") do echo     %%~zF bytes  %%~tF
pause
