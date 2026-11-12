using GpOne.Api.Agenti;
using GpOne.Api.Ai;
using GpOne.Api.Auth;
using GpOne.Api.Clienti;
using GpOne.Api.Domande;
using GpOne.Api.Infrastruttura;
using GpOne.Api.Schema;
using GpOne.Core;
using GpOne.Motore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// GpOne.Api.exe --cifra-password [password | --file <percorso>]: valore da mettere in Password= di ConnectionStrings:GpOne
// (vedi CifraPassword.bat). Va eseguito sullo stesso server e con la stessa CartellaDati dell'applicazione.
// In questa modalità non si scrive alcun log, così su stdout esce solo il valore cifrato.
var modoCifra = ComandoCifra.Richiesto(args, "--cifra-password");

// Log su file (Logs accanto all'eseguibile) attivo da subito: registra anche gli errori di avvio,
// per esempio un appsettings.json non valido o una password non decifrabile.
Log.Logger = modoCifra ? Serilog.Core.Logger.None : LogSuFile.Crea("gpone-api");

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (modoCifra)
        builder.Logging.ClearProviders();
    else
        builder.Logging.AddSerilog(Log.Logger);

    // Chiavi di cifratura e chiave JWT: GpOne:CartellaDati (predefinita App_Data), protette con DPAPI.
    // In produzione: cartella fuori dal sito, leggibile solo dall'account del servizio.
    var cartellaDati = builder.Configuration["GpOne:CartellaDati"] is { Length: > 0 } cartella
        ? cartella
        : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("GpOne")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(cartellaDati, "keys")));
    if (OperatingSystem.IsWindows())
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);

    builder.Services.AddSingleton<Cifratura>();
    builder.Services.AddSingleton<GpOneDatabase>();
    builder.Services.AddSingleton(sp => new TokenService(cartellaDati, sp.GetRequiredService<IDataProtectionProvider>(), builder.Configuration));
    builder.Services.AddSingleton<AgentiRegistro>();
    builder.Services.AddSingleton<AgenteClient>();
    builder.Services.AddScoped<ClientiAccesso>();
    builder.Services.AddScoped<SchemaService>();
    builder.Services.AddScoped<ConfigurazioneAiService>();
    builder.Services.AddSingleton<ClaudeGeneratore>();
    builder.Services.AddSingleton(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        return new OllamaGeneratore(() => factory.CreateClient(nameof(OllamaGeneratore)));
    });
    builder.Services.AddSingleton(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        return new GeminiGeneratore(() => factory.CreateClient(nameof(GeminiGeneratore)));
    });
    builder.Services.AddSingleton<IGeneratoreSql, GeneratoreSql>();
    builder.Services.AddSingleton(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        return new ElencoModelli(() => factory.CreateClient(nameof(ElencoModelli)));
    });
    builder.Services.AddHttpClient(nameof(ElencoModelli), c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddScoped<DomandeService>();
    builder.Services.AddHttpClient(nameof(OllamaGeneratore), c => c.Timeout = TimeSpan.FromMinutes(5));
    builder.Services.AddHttpClient(nameof(GeminiGeneratore), c => c.Timeout = TimeSpan.FromMinutes(5));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<TokenService>((options, tokens) => options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = TokenService.Emittente,
            ValidAudience = TokenService.Emittente,
            IssuerSigningKey = tokens.ChiaveFirma,
            ClockSkew = TimeSpan.FromMinutes(1)
        });
    builder.Services.AddAuthorization();

    // Gli agenti dei clienti si collegano qui; i risultati delle query possono essere grandi
    builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 64 * 1024 * 1024;
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    });

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (modoCifra)
        return ComandoCifra.Esegui(args, app.Services.GetRequiredService<Cifratura>().Cifra);

    if (!await VerificaAvvio.EseguiAsync(app, cartellaDati))
    {
        Log.Fatal("Avvio di GpOne.Api interrotto: correggere la configurazione indicata sopra e riavviare.");
        Console.Error.WriteLine($"Avvio interrotto: vedere il log in {LogSuFile.Cartella}");
        return 1;
    }

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();
    else
        app.UseExceptionHandler();

    app.UseHttpsRedirection();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<AgentiHub>(ProtocolloAgente.PercorsoHub);
    // Frontend Angular (build copiata in wwwroot): le rotte del client tornano a index.html
    app.MapFallbackToFile("index.html");

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Errori prima che l'applicazione sia in piedi (es. appsettings.json non valido, porta già in uso)
    Log.Fatal(ex, "GpOne.Api non avviata: {Messaggio}", ex.Message);
    Console.Error.WriteLine($"GpOne.Api non avviata: {ex.Message}");
    if (!modoCifra)
        Console.Error.WriteLine($"Dettagli nel log in {LogSuFile.Cartella}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
