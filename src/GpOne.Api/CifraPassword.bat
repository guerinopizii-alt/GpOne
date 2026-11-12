@echo off
setlocal
rem ==========================================================================================
rem  GpOne.Api - cifra una password (es. quella di ConnectionStrings:GpOne in appsettings.json)
rem
rem  Chiede la password (non viene mostrata) e scrive il valore "ENC:..." in password-cifrata.txt,
rem  accanto a questo file. Copiare il valore dopo "Password=" nella stringa di connessione e
rem  cancellare il file.
rem
rem  Eseguirlo sul server dove gira l'API: la cifratura usa le chiavi di GpOne:CartellaDati,
rem  quindi il valore funziona solo con quella installazione.
rem ==========================================================================================
cd /d "%~dp0"
set "USCITA=%~dp0password-cifrata.txt"

if exist "GpOne.Api.exe" (
    "GpOne.Api.exe" --cifra-password --file "%USCITA%"
) else if exist "GpOne.Api.csproj" (
    echo Compilazione di GpOne.Api in corso...
    dotnet build "GpOne.Api.csproj" -nologo -v q >nul
    if errorlevel 1 (
        echo Compilazione non riuscita: eseguire "dotnet build" per i dettagli.
        goto fine
    )
    dotnet "bin\Debug\net9.0\GpOne.Api.dll" --cifra-password --file "%USCITA%"
) else (
    echo GpOne.Api.exe non trovato: copiare questo file nella cartella dell'API.
    goto fine
)

if errorlevel 1 (
    echo.
    echo Nessun valore generato.
) else (
    echo.
    echo Copiare il contenuto di password-cifrata.txt dopo "Password=" in appsettings.json,
    echo poi cancellare il file.
)

:fine
echo.
pause
