@echo off
rem Rebuild the single-file release package into the PROJECT ROOT: .\EasyerLauncher.exe
rem There is exactly ONE launcher to double-click. Run this after ANY code change,
rem otherwise the exe stays old. Debug build lives in EasyerLauncher\bin\Debug (for breakpoints only).
cd /d "%~dp0"

taskkill /F /IM EasyerLauncher.exe >nul 2>&1

dotnet publish EasyerLauncher\EasyerLauncher.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None -p:DebugSymbols=false ^
  -o .

if errorlevel 1 (
  echo.
  echo [FAIL] publish failed
  pause
  exit /b 1
)

echo.
echo [OK] .\EasyerLauncher.exe
for %%F in ("EasyerLauncher.exe") do echo     %%~zF bytes  %%~tF
pause
