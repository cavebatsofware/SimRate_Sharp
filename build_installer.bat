@echo off
REM Build script for SimRate Sharp installer

echo Building SimRate Sharp...
echo.

REM Build the application in Release mode
echo [1/3] Building application...
dotnet build SimRateSharp\SimRateSharp.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo [2/3] Checking for Inno Setup...

REM Check if Inno Setup is installed
set INNO_SETUP="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO_SETUP% (
    echo ERROR: Inno Setup not found at %INNO_SETUP%
    echo Please install Inno Setup from https://jrsoftware.org/isinfo.php
    exit /b 1
)

echo.
echo [3/3] Building installer...
%INNO_SETUP% installer.iss
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Installer build failed
    exit /b 1
)

echo.
echo ========================================
echo SUCCESS!
echo Installer created in: installer_output\
echo ========================================
