@echo off
setlocal
set AVALONIA_TELEMETRY_OPTOUT=1

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$pidFile=Join-Path (Get-Location) '.nsbd-desktop.pid'; if (Test-Path $pidFile) { $appId=[int](Get-Content $pidFile); if (Get-Process -Id $appId -ErrorAction SilentlyContinue) { Stop-Process -Id $appId -Force; Remove-Item $pidFile -Force; exit 0 } Remove-Item $pidFile -Force -ErrorAction SilentlyContinue }; exit 1"
if not errorlevel 1 (
    echo NSBD desktop stopped.
    endlocal
    exit /b 0
)

dotnet build NSBD.Desktop\NSBD.Desktop.csproj --nologo
if errorlevel 1 (
    echo Build failed. The desktop app was not started.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$root=(Get-Location).Path; $exe=Join-Path $root 'NSBD.Desktop\bin\Debug\net10.0\NSBD.Desktop.exe'; $workingDirectory=Split-Path $exe; $app=Start-Process -FilePath $exe -WorkingDirectory $workingDirectory -PassThru; Set-Content -Path (Join-Path $root '.nsbd-desktop.pid') -Value $app.Id"

echo NSBD desktop is running.
echo Run this same file again to stop it.

endlocal
