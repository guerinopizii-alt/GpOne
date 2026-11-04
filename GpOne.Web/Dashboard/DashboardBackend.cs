using DevExpress.AspNetCore;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardWeb;
using DevExpress.DataAccess.ConnectionParameters;
using DevExpress.DataAccess.Web;
using GpOne.Services;
using Microsoft.AspNetCore.DataProtection;

namespace GpOne.Web.Dashboard;

/// <summary>API delle dashboard in sola visualizzazione (tutti gli utenti collegati).</summary>
public class DashboardViewerController(DashboardConfigurator configurator, IDataProtectionProvider? dataProtectionProvider = null)
    : RestrictedDashboardController(configurator, dataProtectionProvider);

/// <summary>API del designer delle dashboard (solo amministratori, verificato dal middleware).</summary>
public class DashboardDesignerController(DashboardConfigurator configurator, IDataProtectionProvider? dataProtectionProvider = null)
    : DashboardController(configurator, dataProtectionProvider);

/// <summary>
/// Le dashboard usano le connessioni configurate in GpOne: al designer arriva solo il nome,
/// la stringa di connessione (con la password) resta sul server.
/// </summary>
public class GpOneConnectionStringsProvider(ConnectionProfileStore profili) : IDataSourceWizardConnectionStringsProvider
{
    public Dictionary<string, string> GetConnectionDescriptions() =>
        profili.Nomi.ToDictionary(nome => nome, nome => nome);

    public DataConnectionParametersBase? GetDataConnectionParameters(string name) =>
        profili.GetConnectionString(name) is { } connectionString
            ? new CustomStringConnectionParameters($"XpoProvider=MSSqlServer;{connectionString}")
            : null;
}

public static class DashboardBackend
{
    public static void AddGpOneDashboard(this IServiceCollection services, string cartellaDati)
    {
        var cartella = Path.Combine(cartellaDati, "Dashboards");
        Directory.CreateDirectory(cartella);

        services.AddDevExpressControls();
        services.AddControllers();
        services.AddSingleton(new DashboardOpzioni("api/dashboard", "api/dashboard-designer", cartella));
        services.AddScoped(sp =>
        {
            var configurator = new DashboardConfigurator();
            configurator.SetDashboardStorage(new DashboardFileStorage(cartella));
            configurator.SetConnectionStringsProvider(new GpOneConnectionStringsProvider(sp.GetRequiredService<ConnectionProfileStore>()));
            return configurator;
        });
    }

    /// <summary>
    /// Le API delle dashboard rispondono solo con un codice valido rilasciato a un utente collegato a GpOne;
    /// quelle del designer solo agli amministratori.
    /// </summary>
    public static void UseGpOneDashboard(this WebApplication app)
    {
        app.UseDevExpressControls();

        app.Use(async (context, next) =>
        {
            var percorso = context.Request.Path;
            if (percorso.StartsWithSegments("/api/dashboard") || percorso.StartsWithSegments("/api/dashboard-designer"))
            {
                var accessi = context.RequestServices.GetRequiredService<DashboardAccessi>();
                if (!accessi.Valido(context.Request.Headers[DashboardOpzioni.Intestazione], out var amministratore)
                    || (percorso.StartsWithSegments("/api/dashboard-designer") && !amministratore))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next();
        });

        app.MapDashboardRoute("api/dashboard", "DashboardViewer");
        app.MapDashboardRoute("api/dashboard-designer", "DashboardDesigner");
    }
}
