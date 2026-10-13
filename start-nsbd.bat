@echo off
setlocal

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$pidFile=Join-Path (Get-Location) '.nsbd.pid'; if (Test-Path $pidFile) { $serverId=[int](Get-Content $pidFile); if (Get-Process -Id $serverId -ErrorAction SilentlyContinue) { Stop-Process -Id $serverId -Force; Remove-Item $pidFile -Force; exit 0 } Remove-Item $pidFile -Force -ErrorAction SilentlyContinue }; exit 1"
if not errorlevel 1 (
    echo NSBD server stopped.
    endlocal
    exit /b 0
)

dotnet build NSBD.Web\NSBD.Web.csproj --nologo
if errorlevel 1 (
    echo Build failed. The server was not started.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$root=(Get-Location).Path; $exe=Join-Path $root 'NSBD.Web\bin\Debug\net10.0\NSBD.Web.exe'; $workingDirectory=Split-Path $exe; $process=Start-Process -FilePath $exe -ArgumentList @('--urls','http://localhost:5088') -WorkingDirectory $workingDirectory -WindowStyle Minimized -PassThru; Set-Content -Path (Join-Path $root '.nsbd.pid') -Value $process.Id"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ready=$false; 1..20 | ForEach-Object { try { Invoke-WebRequest -Uri 'http://localhost:5088' -UseBasicParsing -TimeoutSec 1 | Out-Null; $ready=$true; break } catch { Start-Sleep -Milliseconds 250 } }; if (-not $ready) { exit 1 }"
if errorlevel 1 (
    echo The server did not start correctly.
    pause
    exit /b 1
)

if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" (
    start "" "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --disable-gpu --disable-extensions --new-window --user-data-dir="%~dp0.nsbd-browser" http://localhost:5088
) else if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" (
    start "" "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" --disable-gpu --disable-extensions --new-window --user-data-dir="%~dp0.nsbd-browser" http://localhost:5088
) else if exist "%LocalAppData%\Google\Chrome\Application\chrome.exe" (
    start "" "%LocalAppData%\Google\Chrome\Application\chrome.exe" --disable-gpu --disable-extensions --new-window --user-data-dir="%~dp0.nsbd-browser" http://localhost:5088
) else (
    start "" http://localhost:5088
)
echo NSBD is running at http://localhost:5088
echo Run this same file again to stop it.

endlocal
