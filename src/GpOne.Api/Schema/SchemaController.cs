using GpOne.Api.Auth;
using GpOne.Motore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GpOne.Api.Schema;

[ApiController]
[Route("api/admin/clienti/{clienteId:int}/schema")]
[Authorize(Roles = TokenService.RuoloAmministratore)]
public class SchemaController(SchemaService schema) : ControllerBase
{
    [HttpGet]
    public Task<List<OggettoConfigurato>> Elenco(int clienteId) => schema.ElencoAsync(clienteId);

    /// <summary>Rilegge la struttura dal DB del cliente tramite l'agente.</summary>
    [HttpPost("leggi")]
    public async Task<IActionResult> Leggi(int clienteId, CancellationToken cancellationToken)
    {
        try
        {
            var oggetti = await schema.AggiornaDaAgenteAsync(clienteId, cancellationToken);
            return Ok(new { oggetti });
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or Microsoft.AspNetCore.SignalR.HubException)
        {
            return Problem($"Lettura della struttura non riuscita: {ex.Message}", statusCode: 502);
        }
    }

    [HttpPut]
    public async Task<IActionResult> Salva(int clienteId, List<OggettoConfigurato> oggetti)
    {
        await schema.SalvaAsync(clienteId, oggetti);
        return NoContent();
    }
}
