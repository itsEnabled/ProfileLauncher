@echo off
rem Builds the one file you hand out: dist\ProfileLauncher.exe
setlocal
cd /d "%~dp0"

echo Building Profile Launcher...
echo.
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none ^
  -o "%~dp0dist"

if errorlevel 1 (
  echo.
  echo BUILD FAILED. If the error says the file is in use, quit Profile Launcher
  echo from its tray icon and run this again.
  pause
  exit /b 1
)

echo.
echo ------------------------------------------------------------
for %%F in ("%~dp0dist\ProfileLauncher.exe") do echo  Ready: %%~fF  (%%~zF bytes)
echo  This single file is the whole app. Share this one, nothing else.
echo ------------------------------------------------------------
explorer "%~dp0dist"
pause
