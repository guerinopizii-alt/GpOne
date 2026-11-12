using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using GpOne.Api.Auth;
using GpOne.Api.Clienti;
using GpOne.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GpOne.Api.Domande;

public record DomandaRichiesta(string Testo);
public record RiprovaRichiesta(string? Commento);
public record ChiarimentoRichiesta(string Risposta);
public record ValutazioneRichiesta(bool Corretta);

/// <summary>Domande sui dati del cliente scelto (header X-Cliente-Id, verificato contro gli abbinamenti dell'utente).</summary>
[ApiController]
[Route("api/domande")]
[Authorize]
public class DomandeController(DomandeService domande, ClientiAccesso accesso) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RispostaDomanda>> Chiedi(DomandaRichiesta richiesta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(richiesta.Testo))
            return Problem("Scrivere la domanda.", statusCode: 400);
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        try
        {
            return await domande.ChiediAsync(User.UtenteId(), cliente.Id, richiesta.Testo, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException)
        {
            return Problem(ex.Message, statusCode: 502);
        }
    }

    /// <summary>"Risposta sbagliata, riprova": segna la risposta come sbagliata con il commento e ne genera una nuova.</summary>
    [HttpPost("{id:long}/riprova")]
    public async Task<ActionResult<RispostaDomanda>> Riprova(long id, RiprovaRichiesta richiesta, CancellationToken cancellationToken)
    {
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        try
        {
            return await domande.RiprovaAsync(id, User.UtenteId(), cliente.Id, richiesta.Commento, cancellationToken) is { } risposta
                ? risposta
                : Problem("Domanda non trovata.", statusCode: 404);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException)
        {
            return Problem(ex.Message, statusCode: 502);
        }
    }

    /// <summary>Risposta dell'utente al chiarimento chiesto dall'AI: genera una nuova risposta.</summary>
    [HttpPost("{id:long}/chiarimento")]
    public async Task<ActionResult<RispostaDomanda>> Chiarimento(long id, ChiarimentoRichiesta richiesta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(richiesta.Risposta))
            return Problem("Scrivere la risposta.", statusCode: 400);
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        try
        {
            return await domande.RispondiChiarimentoAsync(id, User.UtenteId(), cliente.Id, richiesta.Risposta, cancellationToken) is { } risposta
                ? risposta
                : Problem("Domanda non trovata.", statusCode: 404);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException)
        {
            return Problem(ex.Message, statusCode: 502);
        }
    }

    /// <summary>Conferma la risposta come corretta (diventa un esempio per l'AI) o, con corretta = false, annulla la conferma.</summary>
    [HttpPut("{id:long}/valutazione")]
    public async Task<IActionResult> Valuta(long id, ValutazioneRichiesta richiesta)
    {
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        return await domande.ValutaAsync(id, User.UtenteId(), cliente.Id, richiesta.Corretta ? Valutazioni.Corretta : null)
            ? NoContent()
            : Problem("Domanda non trovata o non confermabile (solo le risposte eseguite con successo).", statusCode: 404);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VoceStorico>>> Storico(int limite = 50)
    {
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);
        return Ok(await domande.StoricoAsync(User.UtenteId(), cliente.Id, limite));
    }

    /// <summary>Riapre una domanda dello storico rieseguendo la query salvata (senza chiamare l'AI).</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<RispostaDomanda>> Apri(long id, CancellationToken cancellationToken)
    {
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        try
        {
            return await domande.ApriAsync(id, User.UtenteId(), cliente.Id, cancellationToken) is { } risposta
                ? risposta
                : Problem("Domanda non trovata.", statusCode: 404);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException)
        {
            return Problem(ex.Message, statusCode: 502);
        }
    }

    /// <summary>Esporta in Excel o CSV: la query viene rieseguita, i risultati non sono mai salvati da GpOne.</summary>
    [HttpGet("{id:long}/export")]
    public async Task<IActionResult> Export(long id, string formato = "xlsx", CancellationToken cancellationToken = default)
    {
        if (await accesso.ClienteRichiestaAsync(HttpContext) is not { } cliente)
            return Problem("Cliente non selezionato o non accessibile.", statusCode: 403);

        string testo;
        RisultatoQuery risultato;
        try
        {
            (testo, risultato) = await domande.RieseguiAsync(id, User.UtenteId(), cliente.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException)
        {
            return Problem(ex.Message, statusCode: 502);
        }

        var nome = $"GpOne_{cliente.Codice}_{DateTime.Now:yyyyMMdd_HHmm}";
        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv(risultato))).ToArray(), "text/csv", $"{nome}.csv");

        using var workbook = new XLWorkbook();
        var foglio = workbook.Worksheets.Add("Risultato");
        for (var c = 0; c < risultato.Colonne.Count; c++)
            foglio.Cell(1, c + 1).Value = risultato.Colonne[c].Nome;
        for (var r = 0; r < risultato.Righe.Count; r++)
            for (var c = 0; c < risultato.Colonne.Count; c++)
                foglio.Cell(r + 2, c + 1).Value = ValoreExcel(risultato.Righe[r][c]);
        foglio.Row(1).Style.Font.Bold = true;
        foglio.Columns().AdjustToContents(1, Math.Min(risultato.Righe.Count + 1, 200));
        workbook.Worksheets.Add("Domanda").Cell(1, 1).Value = testo;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{nome}.xlsx");
    }

    private static string Csv(RisultatoQuery risultato)
    {
        static string Campo(string? s) => s is null ? "" : s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', risultato.Colonne.Select(c => Campo(c.Nome))));
        foreach (var riga in risultato.Righe)
            sb.AppendLine(string.Join(';', riga.Select(v => Campo(Testo(v)))));
        return sb.ToString();
    }

    // Le righe arrivano dall'agente via SignalR (JSON): i valori sono JsonElement
    private static string? Testo(object? valore) => valore switch
    {
        null => null,
        JsonElement { ValueKind: JsonValueKind.Null } => null,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } e => e.TryGetDecimal(out var d) ? d.ToString(CultureInfo.GetCultureInfo("it-IT")) : e.GetRawText(),
        JsonElement e => e.GetRawText(),
        IFormattable f => f.ToString(null, CultureInfo.GetCultureInfo("it-IT")),
        _ => valore.ToString()
    };

    private static XLCellValue ValoreExcel(object? valore) => valore switch
    {
        JsonElement { ValueKind: JsonValueKind.Number } e when e.TryGetDecimal(out var d) => d,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.String } e when e.TryGetDateTime(out var data) => data,
        _ => Testo(valore) is { } s ? s : Blank.Value
    };
}
