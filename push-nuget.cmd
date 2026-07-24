@echo off
setlocal
cd /d "%~dp0"

if "%NUGET_API_KEY%"=="" (
  echo NUGET_API_KEY is not set.
  echo Run this first in the same terminal:
  echo   set NUGET_API_KEY=your-api-key
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0push-nuget.ps1" %*
set EXIT_CODE=%ERRORLEVEL%

if not "%EXIT_CODE%"=="0" (
  echo.
  echo NuGet publishing failed. Exit code: %EXIT_CODE%
  pause
  exit /b %EXIT_CODE%
)

pause
