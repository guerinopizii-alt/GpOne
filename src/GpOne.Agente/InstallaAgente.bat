@echo off
rem ==========================================================================================
rem  GpOne.Agente - installazione guidata come servizio Windows (avvia InstallaAgente.ps1)
rem
rem  Doppio clic: chiede i permessi di amministratore e poi i dati di configurazione.
rem  Parametri facoltativi passati allo script, es.:
rem    InstallaAgente.bat -SoloConfigurazione     (solo appsettings.json, senza servizio)
rem    InstallaAgente.bat -Disinstalla            (rimuove il servizio)
rem ==========================================================================================
setlocal
cd /d "%~dp0"

net session >nul 2>&1
if errorlevel 1 (
    echo Richiesta dei permessi di amministratore...
    if "%~1"=="" (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    ) else (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%*' -Verb RunAs"
    )
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0InstallaAgente.ps1" %*
echo.
pause
