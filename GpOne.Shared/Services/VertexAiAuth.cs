using System.Collections.Concurrent;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace GpOne.Services;

/// <summary>
/// Autenticazione verso Google Cloud Vertex AI con la chiave JSON di un service account:
/// produce i token OAuth (rinnovati automaticamente) per chiamare l'endpoint regionale.
/// </summary>
internal static class VertexAiAuth
{
    private const string Scope = "https://www.googleapis.com/auth/cloud-platform";

    // Una credenziale per chiave: la libreria Google rinnova e riusa i token fino alla scadenza
    private static readonly ConcurrentDictionary<string, GoogleCredential> Credenziali = new();

    public static Task<string> AccessTokenAsync(string chiaveJson, CancellationToken cancellationToken)
    {
        var credenziale = Credenziali.GetOrAdd(chiaveJson, json =>
#pragma warning disable CS0618 // FromJson è sconsigliato per JSON non attendibili: qui la chiave la fornisce l'amministratore
            GoogleCredential.FromJson(json).CreateScoped(Scope));
#pragma warning restore CS0618
        return credenziale.UnderlyingCredential.GetAccessTokenForRequestAsync(null, cancellationToken);
    }

    /// <summary>ID del progetto Google Cloud letto dalla chiave del service account.</summary>
    public static string? ProgettoId(string? chiaveJson)
    {
        if (string.IsNullOrWhiteSpace(chiaveJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(chiaveJson);
            return doc.RootElement.TryGetProperty("project_id", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
