@echo off
setlocal
rem ==========================================================================================
rem  GpOne.Agente - cifra la chiave dell'agente o la password del database del cliente
rem
rem  Chiede il valore (non viene mostrato) e scrive "ENC:..." in password-cifrata.txt, accanto a
rem  questo file. In appsettings.json dell'agente:
rem    - chiave dell'agente      -> "Chiave": "ENC:..."  (sezione GpOneAgente)
rem    - password del database   -> dopo "Password=" in ConnectionStrings:Cliente
rem  Poi cancellare il file.
rem
rem  Eseguirlo sul computer dove gira l'agente: la cifratura (DPAPI) vale solo su quel computer.
rem ==========================================================================================
cd /d "%~dp0"
set "USCITA=%~dp0password-cifrata.txt"

if exist "GpOne.Agente.exe" (
    "GpOne.Agente.exe" --cifra --file "%USCITA%"
) else if exist "GpOne.Agente.csproj" (
    echo Compilazione di GpOne.Agente in corso...
    dotnet build "GpOne.Agente.csproj" -nologo -v q >nul
    if errorlevel 1 (
        echo Compilazione non riuscita: eseguire "dotnet build" per i dettagli.
        goto fine
    )
    "bin\Debug\net9.0-windows\GpOne.Agente.exe" --cifra --file "%USCITA%"
) else (
    echo GpOne.Agente.exe non trovato: copiare questo file nella cartella dell'agente.
    goto fine
)

if errorlevel 1 (
    echo.
    echo Nessun valore generato.
) else (
    echo.
    echo Copiare il contenuto di password-cifrata.txt in appsettings.json dell'agente
    echo ^(Chiave oppure Password= della stringa di connessione^), poi cancellare il file.
)

:fine
echo.
pause
