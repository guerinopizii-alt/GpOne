using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace GpOne.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra l'archivio dei profili di connessione e la sessione utente.
    /// In <paramref name="cartellaDati"/> vengono salvati i profili (connessioni.json) e le chiavi di cifratura (keys).
    /// </summary>
    /// <param name="chiaviPerMacchina">
    /// true: chiavi protette con DPAPI a livello di macchina (server web);
    /// false: a livello dell'utente Windows corrente (desktop).
    /// </param>
    public static IServiceCollection AddGpOneSessione(this IServiceCollection services, string cartellaDati, bool chiaviPerMacchina)
    {
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("GpOne")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(cartellaDati, "keys")));

        if (OperatingSystem.IsWindows())
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: chiaviPerMacchina);

        services.AddSingleton(sp => new ConnectionProfileStore(
            Path.Combine(cartellaDati, "connessioni.json"),
            sp.GetRequiredService<IDataProtectionProvider>()));
        services.AddSingleton<DatabaseSetupService>();
        services.AddScoped<UserSessionService>();
        services.AddScoped<UtentiService>();
        services.AddSingleton<DashboardAccessi>();

        return services;
    }

    /// <summary>
    /// Registra GpOne AI con il motore AI scelto dall'amministratore (impostazioni in ai.json, chiave API cifrata).
    /// Richiede la Data Protection registrata da <see cref="AddGpOneSessione"/>.
    /// </summary>
    /// <param name="finestraWindows">Solo desktop: handle della finestra principale, per l'accesso con l'account Windows.</param>
    public static IServiceCollection AddGpOneAi(this IServiceCollection services, string cartellaDati, Func<IntPtr>? finestraWindows = null)
    {
        services.AddSingleton(new Microsoft365Opzioni(finestraWindows));
        // Nessun timeout: le risposte arrivano in streaming e si interrompono con il CancellationToken
        services.AddHttpClient(GpOneAiService.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);

        services.AddSingleton(new AiSettingsOpzioni(Path.Combine(cartellaDati, "ai.json")));
        services.AddScoped<AiSettingsStore>();
        services.AddScoped<Microsoft365CopilotService>();
        services.AddScoped<GpOneAiService>();
        services.AddScoped<IAiChatService>(sp => sp.GetRequiredService<GpOneAiService>());
        services.AddScoped<DatiAiService>();
        services.AddScoped<ConversazioniService>();
        services.AddScoped<DomandeAiService>();
        services.AddScoped<LogInviiService>();
        services.AddScoped<CopiaTabelleService>();

        return services;
    }
}
