using GpOne.Agente;
using GpOne.Core;
using Serilog;

// GpOne.Agente.exe --cifra [testo | --file <percorso>]: valore "ENC:..." per la chiave agente o la password del DB
// in appsettings.json (vedi CifraPassword.bat). Cifrato con DPAPI a livello macchina: decifrabile solo su questo computer.
if (ComandoCifra.Richiesto(args, "--cifra"))
    return ComandoCifra.Esegui(args, Segreti.Cifra);

// Log su file (Logs accanto all'eseguibile) attivo da subito, anche per gli errori di avvio
Log.Logger = LogSuFile.Crea("gpone-agente");
try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.AddSerilog(Log.Logger);
    builder.Services.AddWindowsService(options => options.ServiceName = "GpOne Agente");
    builder.Services.Configure<OpzioniAgente>(builder.Configuration.GetSection("GpOneAgente"));
    builder.Services.AddSingleton<DatabaseCliente>();
    builder.Services.AddHostedService<AgenteWorker>();

    await builder.Build().RunAsync();
    return Environment.ExitCode;
}
catch (Exception ex)
{
    // Errori prima che l'agente sia in piedi (es. appsettings.json non valido)
    Log.Fatal(ex, "GpOne.Agente non avviato: {Messaggio}", ex.Message);
    Console.Error.WriteLine($"GpOne.Agente non avviato: {ex.Message}\nDettagli nel log in {LogSuFile.Cartella}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
