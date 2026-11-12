using Serilog;

namespace GpOne.Api.Infrastruttura;

/// <summary>
/// Log su file giornaliero in &lt;cartella dell'applicazione&gt;\Logs (gpone-api-AAAAMMGG.log), conservati 30 giorni.
/// Si aggiunge ai log su console: i livelli si regolano come sempre con Logging:LogLevel in appsettings.json.
/// </summary>
public static class LogSuFile
{
    public static readonly string Cartella = Path.Combine(AppContext.BaseDirectory, "Logs");

    public static Serilog.ILogger Crea(string nome) =>
        new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(Path.Combine(Cartella, nome + "-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 20_000_000,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
}
