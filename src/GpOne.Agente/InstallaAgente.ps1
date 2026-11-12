<#
.SYNOPSIS
    Installa (o aggiorna, o rimuove) GpOne Agente come servizio Windows.

.DESCRIPTION
    Da eseguire come amministratore, sul computer dove girerà l'agente, dalla cartella che contiene GpOne.Agente.exe
    (più comodo: doppio clic su InstallaAgente.bat, che chiede i permessi di amministratore).

    1. Chiede i dati mancanti: indirizzo di GpOne, ClienteId, chiave dell'agente, database del cliente.
       Rieseguendolo propone i valori già configurati (Invio = mantieni, anche per chiave e password).
    2. Verifica che GpOne sia raggiungibile e che il database del cliente accetti le credenziali.
    3. Cifra chiave e password con DPAPI a livello macchina (valori "ENC:...", decifrabili solo su questo computer)
       e scrive appsettings.json (la versione precedente resta come copia .bak).
    4. Crea o aggiorna il servizio (avvio automatico ritardato, riavvio automatico in caso di errore), lo avvia
       e controlla nel log che l'agente si colleghi a GpOne.

.PARAMETER ServerUrl
    Indirizzo di GpOne, es. https://gpone.miodominio.it (per una prova in rete locale anche http://192.168.1.20:5188).
.PARAMETER ClienteId
    ID del cliente in GpOne (Amministrazione > Clienti).
.PARAMETER SqlServer
    Istanza SQL Server del cliente, es. "." o "SERVERSQL\ISTANZA".
.PARAMETER Database
    Nome del database del cliente.
.PARAMETER SqlUtente
    Utente SQL di sola lettura (consigliato: membro del solo ruolo db_datareader).
.PARAMETER AutenticazioneWindows
    Usa l'autenticazione Windows invece dell'utente SQL: il servizio gira come LocalSystem, quindi su SQL Server
    serve un login per l'account del computer (DOMINIO\NOMEPC$, oppure NT AUTHORITY\SYSTEM se SQL è su questo computer).
.PARAMETER SoloConfigurazione
    Scrive appsettings.json senza creare il servizio (per provare l'agente a mano lanciando GpOne.Agente.exe).
.PARAMETER Disinstalla
    Ferma e rimuove il servizio (i file e la configurazione restano nella cartella).

.EXAMPLE
    .\InstallaAgente.ps1
    Installazione guidata: chiede tutti i dati.
.EXAMPLE
    .\InstallaAgente.ps1 -ServerUrl https://gpone.miodominio.it -ClienteId 5 -SqlServer SERVERSQL -Database Gestionale -SqlUtente gpone_lettura
    Chiede solo chiave dell'agente e password SQL (mai passate come parametro: resterebbero nella cronologia dei comandi).
.EXAMPLE
    .\InstallaAgente.ps1 -Disinstalla
#>
[CmdletBinding()]
param(
    [string]$ServerUrl,
    [int]$ClienteId,
    [string]$SqlServer,
    [string]$Database,
    [string]$SqlUtente,
    [switch]$AutenticazioneWindows,
    [switch]$SoloConfigurazione,
    [switch]$Disinstalla,
    [string]$NomeServizio = 'GpOne Agente'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$cartella = $PSScriptRoot
$eseguibile = Join-Path $cartella 'GpOne.Agente.exe'
$fileConfigurazione = Join-Path $cartella 'appsettings.json'
$cartellaLog = Join-Path $cartella 'Logs'

# ---------------------------------------------------------------------------------------------------------------
# Funzioni di supporto
# ---------------------------------------------------------------------------------------------------------------

function Scrivi-Titolo([string]$testo) { Write-Host ''; Write-Host "== $testo ==" -ForegroundColor Magenta }
function Scrivi-Ok([string]$testo) { Write-Host "  [OK] $testo" -ForegroundColor Green }
function Scrivi-Avviso([string]$testo) { Write-Host "  [!]  $testo" -ForegroundColor Yellow }
function Scrivi-Errore([string]$testo) { Write-Host "  [X]  $testo" -ForegroundColor Red }

function Esci-ConErrore([string]$testo) {
    Scrivi-Errore $testo
    exit 1
}

function Chiedi-Valore([string]$domanda, [string]$predefinito) {
    $etichetta = if ($predefinito) { "$domanda [$predefinito]" } else { $domanda }
    do {
        $valore = Read-Host $etichetta
        if ([string]::IsNullOrWhiteSpace($valore)) { $valore = $predefinito }
    } while ([string]::IsNullOrWhiteSpace($valore))
    return $valore.Trim()
}

function Chiedi-SiNo([string]$domanda, [bool]$predefinito) {
    $suggerimento = if ($predefinito) { 'S/n' } else { 's/N' }
    $risposta = Read-Host "$domanda [$suggerimento]"
    if ([string]::IsNullOrWhiteSpace($risposta)) { return $predefinito }
    return $risposta.Trim().ToLowerInvariant().StartsWith('s')
}

function Converti-InChiaro([Security.SecureString]$sicura) {
    $puntatore = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sicura)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntatore) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntatore) }
}

<# Segreto nascosto. Con $attuale (già cifrato) Invio lo mantiene: restituisce $null. #>
function Chiedi-Segreto([string]$domanda, [string]$attuale) {
    $mantieni = $attuale -and $attuale.StartsWith('ENC:')
    $etichetta = if ($mantieni) { "$domanda (Invio = mantieni quella attuale)" } else { $domanda }
    while ($true) {
        $valore = Converti-InChiaro (Read-Host $etichetta -AsSecureString)
        if ($valore) { return $valore }
        if ($mantieni) { return $null }
        Scrivi-Avviso 'Il valore non può essere vuoto.'
    }
}

# Stesso formato di Segreti.Cifra nell'agente: "ENC:" + base64(DPAPI LocalMachine, entropia "GpOne.Agente.v1").
Add-Type -AssemblyName System.Security
function Cifra([string]$testo) {
    $entropia = [Text.Encoding]::UTF8.GetBytes('GpOne.Agente.v1')
    $dati = [Text.Encoding]::UTF8.GetBytes($testo)
    $cifrati = [Security.Cryptography.ProtectedData]::Protect($dati, $entropia, [Security.Cryptography.DataProtectionScope]::LocalMachine)
    return 'ENC:' + [Convert]::ToBase64String($cifrati)
}

function Imposta-Proprieta($oggetto, [string]$nome, $valore) {
    if ($oggetto.PSObject.Properties[$nome]) { $oggetto.$nome = $valore }
    else { $oggetto | Add-Member -NotePropertyName $nome -NotePropertyValue $valore }
}

function Prova-Tcp([string]$nomeHost, [int]$porta) {
    $client = New-Object Net.Sockets.TcpClient
    try { return $client.ConnectAsync($nomeHost, $porta).Wait(5000) -and $client.Connected }
    catch { return $false }
    finally { $client.Dispose() }
}

<# Dimensione attuale dei file di log, per leggere poi solo le righe nuove. #>
function Leggi-PosizioniLog {
    $posizioni = @{}
    if (Test-Path $cartellaLog) {
        Get-ChildItem $cartellaLog -Filter 'gpone-agente-*.log' | ForEach-Object { $posizioni[$_.FullName] = $_.Length }
    }
    return $posizioni
}

function Leggi-NuoveRigheLog($posizioni) {
    $righe = @()
    if (-not (Test-Path $cartellaLog)) { return $righe }
    foreach ($file in Get-ChildItem $cartellaLog -Filter 'gpone-agente-*.log' | Sort-Object LastWriteTime) {
        $inizio = 0
        if ($posizioni.ContainsKey($file.FullName)) { $inizio = $posizioni[$file.FullName] }
        if ($file.Length -le $inizio) { continue }
        # Il log è aperto dall'agente: lettura con condivisione in scrittura
        $flusso = New-Object IO.FileStream($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]'ReadWrite, Delete')
        try {
            [void]$flusso.Seek($inizio, [IO.SeekOrigin]::Begin)
            $lettore = New-Object IO.StreamReader($flusso, [Text.Encoding]::UTF8)
            $righe += $lettore.ReadToEnd() -split "`r?`n" | Where-Object { $_ }
        }
        finally { $flusso.Dispose() }
    }
    return $righe
}

# ---------------------------------------------------------------------------------------------------------------
# Controlli iniziali
# ---------------------------------------------------------------------------------------------------------------

$identita = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identita.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Esci-ConErrore 'Eseguire come amministratore (oppure con doppio clic su InstallaAgente.bat).'
}
if (-not (Test-Path $eseguibile)) {
    Esci-ConErrore "GpOne.Agente.exe non trovato in $cartella : copiare lo script nella cartella dell'agente."
}

$servizio = Get-Service -Name $NomeServizio -ErrorAction SilentlyContinue

if ($Disinstalla) {
    Scrivi-Titolo 'Rimozione del servizio'
    if (-not $servizio) { Scrivi-Ok "Il servizio '$NomeServizio' non è installato."; exit 0 }
    if ($servizio.Status -ne 'Stopped') { Stop-Service -Name $NomeServizio -Force; Scrivi-Ok 'Servizio fermato.' }
    & sc.exe delete "$NomeServizio" | Out-Null
    if ($LASTEXITCODE -ne 0) { Esci-ConErrore "sc.exe delete non riuscito (codice $LASTEXITCODE)." }
    Scrivi-Ok "Servizio '$NomeServizio' rimosso. I file in $cartella non sono stati toccati."
    exit 0
}

Write-Host ''
Write-Host '  GpOne Agente - installazione' -ForegroundColor Magenta
Write-Host "  Cartella: $cartella"

# ---------------------------------------------------------------------------------------------------------------
# Configurazione attuale (per proporre i valori già impostati)
# ---------------------------------------------------------------------------------------------------------------

if (-not (Test-Path $fileConfigurazione)) { Esci-ConErrore "appsettings.json non trovato in $cartella." }
$testoConfigurazione = [IO.File]::ReadAllText($fileConfigurazione)
try { $configurazione = $testoConfigurazione | ConvertFrom-Json }
catch { Esci-ConErrore "appsettings.json non è un JSON valido: $($_.Exception.Message)" }

if (-not $configurazione.PSObject.Properties['GpOneAgente']) {
    Imposta-Proprieta $configurazione 'GpOneAgente' ([pscustomobject]@{ ServerUrl = ''; ClienteId = 0; Chiave = '' })
}
if (-not $configurazione.PSObject.Properties['ConnectionStrings']) {
    Imposta-Proprieta $configurazione 'ConnectionStrings' ([pscustomobject]@{ Cliente = '' })
}
$agente = $configurazione.GpOneAgente

$attuale = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
try {
    if ($configurazione.ConnectionStrings.PSObject.Properties['Cliente'] -and $configurazione.ConnectionStrings.Cliente) {
        $attuale.ConnectionString = $configurazione.ConnectionStrings.Cliente
    }
}
catch { Scrivi-Avviso 'La stringa di connessione attuale non è valida: verrà riscritta.' }

$serverAttuale = if ($agente.PSObject.Properties['ServerUrl'] -and $agente.ServerUrl -notmatch 'localhost:7144') { $agente.ServerUrl } else { '' }
$clienteAttuale = if ($agente.PSObject.Properties['ClienteId'] -and [int]$agente.ClienteId -gt 0) { [string]$agente.ClienteId } else { '' }
$chiaveAttuale = if ($agente.PSObject.Properties['Chiave']) { [string]$agente.Chiave } else { '' }
$passwordAttuale = [string]$attuale.Password
$databaseAttuale = if ($attuale.InitialCatalog -and $attuale.InitialCatalog -ne 'NomeDbCliente') { $attuale.InitialCatalog } else { '' }

# ---------------------------------------------------------------------------------------------------------------
# 1. Collegamento a GpOne
# ---------------------------------------------------------------------------------------------------------------

Scrivi-Titolo '1. Collegamento a GpOne'
while ($true) {
    if (-not $ServerUrl) { $ServerUrl = Chiedi-Valore 'Indirizzo di GpOne (es. https://gpone.miodominio.it)' $serverAttuale }
    $ServerUrl = $ServerUrl.TrimEnd('/')
    $uri = $null
    if ([Uri]::TryCreate($ServerUrl, [UriKind]::Absolute, [ref]$uri) -and ($uri.Scheme -eq 'https' -or $uri.Scheme -eq 'http')) { break }
    Scrivi-Avviso "Indirizzo non valido: '$ServerUrl'. Deve iniziare con https:// (o http:// per le prove)."
    $ServerUrl = $null
}
if ($uri.Scheme -eq 'http' -and -not $uri.IsLoopback) {
    Scrivi-Avviso 'Indirizzo http: chiave e risultati viaggiano in chiaro. Va bene solo per una prova in rete locale.'
}

if (Prova-Tcp $uri.Host $uri.Port) {
    Scrivi-Ok "$($uri.Host) risponde sulla porta $($uri.Port)."
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $stato = Invoke-WebRequest -Uri "$ServerUrl/api/auth/stato" -UseBasicParsing -TimeoutSec 15
        if ($stato.StatusCode -eq 200) { Scrivi-Ok 'GpOne risponde correttamente.' }
    }
    catch {
        Scrivi-Avviso "Il server risponde ma GpOne no: $($_.Exception.Message)"
        if ($uri.Scheme -eq 'https') { Scrivi-Avviso 'Se il problema è il certificato HTTPS, deve essere valido e attendibile su questo computer.' }
        if (-not (Chiedi-SiNo 'Continuare comunque?' $false)) { exit 1 }
    }
}
else {
    Scrivi-Avviso "$($uri.Host):$($uri.Port) non raggiungibile: controllare indirizzo, rete e firewall/proxy in uscita."
    if (-not (Chiedi-SiNo 'Continuare comunque?' $false)) { exit 1 }
}

while ($ClienteId -le 0) {
    $valore = Chiedi-Valore 'ClienteId (Amministrazione > Clienti in GpOne)' $clienteAttuale
    if (-not [int]::TryParse($valore, [ref]$ClienteId) -or $ClienteId -le 0) { Scrivi-Avviso 'Indicare un numero maggiore di zero.'; $ClienteId = 0 }
}
if ([string]$ClienteId -ne $clienteAttuale -and $chiaveAttuale.StartsWith('ENC:')) {
    # La chiave attuale appartiene a un altro cliente: va reinserita
    $chiaveAttuale = ''
}

$chiave = Chiedi-Segreto 'Chiave dell''agente (generata in GpOne con l''icona chiave)' $chiaveAttuale
$chiaveCifrata = if ($chiave) { Cifra $chiave } else { $chiaveAttuale }
$chiave = $null

# ---------------------------------------------------------------------------------------------------------------
# 2. Database del cliente
# ---------------------------------------------------------------------------------------------------------------

Scrivi-Titolo '2. Database del cliente'
$serverPredefinito = if ($attuale.DataSource) { $attuale.DataSource } else { '.' }
if (-not $SqlServer) { $SqlServer = Chiedi-Valore 'Istanza SQL Server (es. . oppure SERVERSQL\ISTANZA)' $serverPredefinito }
if (-not $Database) { $Database = Chiedi-Valore 'Nome del database' $databaseAttuale }

if (-not $PSBoundParameters.ContainsKey('AutenticazioneWindows') -and -not $SqlUtente) {
    $AutenticazioneWindows = [switch](Chiedi-SiNo 'Usare l''autenticazione Windows invece di un utente SQL?' ([bool]$attuale.IntegratedSecurity))
}

$password = $null
if (-not $AutenticazioneWindows) {
    $utentePredefinito = if ($attuale.UserID) { $attuale.UserID } else { 'gpone_lettura' }
    if (-not $SqlUtente) { $SqlUtente = Chiedi-Valore 'Utente SQL (solo lettura)' $utentePredefinito }
    if ($SqlUtente -ne $attuale.UserID) { $passwordAttuale = '' }
    $password = Chiedi-Segreto "Password di $SqlUtente" $passwordAttuale
}

# Prova di collegamento (con autenticazione Windows si prova con l'utente attuale, non con l'account del servizio)
$prova = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$prova['Data Source'] = $SqlServer
$prova['Initial Catalog'] = $Database
$prova['Encrypt'] = $true
$prova['TrustServerCertificate'] = $true
$prova['Connect Timeout'] = 10
$prova['Application Name'] = 'GpOne Agente (installazione)'
$provaPossibile = $true
if ($AutenticazioneWindows) { $prova['Integrated Security'] = $true }
elseif ($password) { $prova['User ID'] = $SqlUtente; $prova['Password'] = $password }
else { $provaPossibile = $false }   # password mantenuta: è cifrata, la verifica la fa l'agente all'avvio

if ($provaPossibile) {
    $connessione = New-Object System.Data.SqlClient.SqlConnection($prova.ConnectionString)
    try {
        $connessione.Open()
        $comando = $connessione.CreateCommand()
        $comando.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
        $tabelle = $comando.ExecuteScalar()
        $comando.CommandText = "SELECT ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0) | ISNULL(IS_ROLEMEMBER('db_owner'), 0) | ISNULL(IS_ROLEMEMBER('db_datawriter'), 0)"
        $permessiAmpi = [int]$comando.ExecuteScalar()
        Scrivi-Ok "Collegato a '$Database' su '$SqlServer': $tabelle tabelle visibili."
        if ($permessiAmpi -ne 0) {
            Scrivi-Avviso 'L''utente ha permessi più ampi della lettura (sysadmin, db_owner o db_datawriter): meglio un utente con il solo ruolo db_datareader.'
        }
        if ($AutenticazioneWindows) {
            Scrivi-Avviso "Prova fatta come $env:USERDOMAIN\$env:USERNAME: il servizio girerà come LocalSystem e si presenterà a SQL Server come $env:USERDOMAIN\$env:COMPUTERNAME`$ (o NT AUTHORITY\SYSTEM se SQL è su questo computer)."
        }
    }
    catch {
        Scrivi-Avviso "Collegamento al database non riuscito: $($_.Exception.GetBaseException().Message)"
        if (-not (Chiedi-SiNo 'Continuare comunque?' $false)) { exit 1 }
    }
    finally { $connessione.Dispose() }
}

$stringa = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$stringa['Data Source'] = $SqlServer
$stringa['Initial Catalog'] = $Database
if ($AutenticazioneWindows) { $stringa['Integrated Security'] = $true }
else {
    $stringa['User ID'] = $SqlUtente
    $stringa['Password'] = if ($password) { Cifra $password } else { $passwordAttuale }
}
$stringa['Encrypt'] = $true
$stringa['TrustServerCertificate'] = $true
$password = $null
$prova = $null

# ---------------------------------------------------------------------------------------------------------------
# 3. Scrittura di appsettings.json
# ---------------------------------------------------------------------------------------------------------------

Scrivi-Titolo '3. Configurazione'
Imposta-Proprieta $agente 'ServerUrl' $ServerUrl
Imposta-Proprieta $agente 'ClienteId' $ClienteId
Imposta-Proprieta $agente 'Chiave' $chiaveCifrata
Imposta-Proprieta $configurazione.ConnectionStrings 'Cliente' $stringa.ConnectionString

# Se il servizio è attivo va fermato prima di toccare la configurazione
if ($servizio -and $servizio.Status -ne 'Stopped') {
    Stop-Service -Name $NomeServizio -Force
    Scrivi-Ok 'Servizio fermato per aggiornare la configurazione.'
}

$copia = "$fileConfigurazione.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Copy-Item $fileConfigurazione $copia
$json = $configurazione | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText($fileConfigurazione, $json, (New-Object Text.UTF8Encoding($false)))
Scrivi-Ok "appsettings.json aggiornato (copia del precedente: $(Split-Path $copia -Leaf))."
Scrivi-Ok 'Chiave e password salvate cifrate (ENC:...), decifrabili solo su questo computer.'

$sviluppo = Join-Path $cartella 'appsettings.Development.json'
if (Test-Path $sviluppo) { Remove-Item $sviluppo; Scrivi-Ok 'Rimosso appsettings.Development.json (serve solo in sviluppo).' }
foreach ($residuo in @('password-cifrata.txt')) {
    $percorso = Join-Path $cartella $residuo
    if (Test-Path $percorso) { Remove-Item $percorso; Scrivi-Ok "Rimosso $residuo." }
}

if ($SoloConfigurazione) {
    Write-Host ''
    Write-Host "Configurazione pronta. Per provare l'agente a mano: `"$eseguibile`" (Ctrl+C per fermarlo)." -ForegroundColor Cyan
    if ($servizio) {
        Write-Host "Il servizio '$NomeServizio' è fermo (due agenti insieme si contendono il collegamento): riavviarlo con Start-Service '$NomeServizio'." -ForegroundColor Yellow
    }
    exit 0
}

# ---------------------------------------------------------------------------------------------------------------
# 4. Servizio Windows
# ---------------------------------------------------------------------------------------------------------------

Scrivi-Titolo '4. Servizio Windows'
$percorsoServizio = "`"$eseguibile`""
if ($servizio) {
    & sc.exe config "$NomeServizio" binPath= $percorsoServizio | Out-Null
    if ($LASTEXITCODE -ne 0) { Esci-ConErrore "Aggiornamento del servizio non riuscito (sc.exe config, codice $LASTEXITCODE)." }
    Scrivi-Ok "Servizio '$NomeServizio' aggiornato."
}
else {
    New-Service -Name $NomeServizio -BinaryPathName $percorsoServizio -DisplayName $NomeServizio -StartupType Automatic `
        -Description 'Esegue sul database locale le interrogazioni di sola lettura richieste da GpOne (connessione in uscita).' | Out-Null
    Scrivi-Ok "Servizio '$NomeServizio' creato."
}
& sc.exe config "$NomeServizio" start= delayed-auto | Out-Null
& sc.exe failure "$NomeServizio" reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
Scrivi-Ok 'Avvio automatico (ritardato) e riavvio automatico in caso di errore.'

$posizioni = Leggi-PosizioniLog
Start-Service -Name $NomeServizio
Scrivi-Ok 'Servizio avviato. Attendo il collegamento a GpOne (massimo 60 secondi)...'

# ---------------------------------------------------------------------------------------------------------------
# 5. Verifica dal log
# ---------------------------------------------------------------------------------------------------------------

$collegato = $false
$segnalate = @{}
$limite = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $limite -and -not $collegato) {
    Start-Sleep -Seconds 2
    foreach ($riga in Leggi-NuoveRigheLog $posizioni) {
        if ($segnalate.ContainsKey($riga)) { continue }
        $segnalate[$riga] = $true
        $messaggio = $riga -replace '^\S+ \S+ \[\w+\] [\w.]+: ', ''
        if ($riga -match 'Collegato a GpOne') { $collegato = $true; Scrivi-Ok $messaggio }
        elseif ($riga -match '\[(ERR|FTL)\]') { Scrivi-Errore $messaggio }
        elseif ($riga -match '\[WRN\]') { Scrivi-Avviso $messaggio }
    }
    if ((Get-Service -Name $NomeServizio).Status -eq 'Stopped') {
        Scrivi-Errore 'Il servizio si è fermato: vedere i messaggi sopra e il log.'
        break
    }
}

Write-Host ''
if ($collegato) {
    Write-Host "Installazione completata: l'agente è collegato a GpOne per il cliente $ClienteId." -ForegroundColor Green
    Write-Host "In GpOne (Clienti) l'agente risulta collegato: ora apri la struttura del cliente e premi 'Rileggi dal database'."
}
else {
    Write-Host 'Il servizio è installato ma il collegamento a GpOne non è ancora confermato.' -ForegroundColor Yellow
    Write-Host 'Il servizio riprova da solo: correggere quanto segnalato e rieseguire lo script, oppure controllare il log.'
}
Write-Host "Log: $cartellaLog"
if (-not $collegato) { exit 2 }
