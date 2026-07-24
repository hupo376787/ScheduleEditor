@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Removing old build output...
for /d /r %%D in (bin,obj) do @if exist "%%D" rd /s /q "%%D"

echo [2/3] Restoring packages into .nuget\packages...
dotnet restore AvaloniaScheduleEditor.sln --packages "%CD%\.nuget\packages" --force --no-cache
if errorlevel 1 goto :failed

echo [3/3] Building Debug configuration...
dotnet build AvaloniaScheduleEditor.sln -c Debug --no-restore
if errorlevel 1 goto :failed

echo.
echo Restore and build succeeded.
pause
exit /b 0

:failed
echo.
echo Restore or build failed. Copy the first error line that contains the denied path.
pause
exit /b 1
