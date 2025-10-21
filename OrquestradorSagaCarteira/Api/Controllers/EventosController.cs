using Microsoft.AspNetCore.Mvc;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventosController : ControllerBase
{
    private readonly IPublicadorEventos _publicadorEventos;
    private readonly ILogger<EventosController> _logger;

    public EventosController(IPublicadorEventos publicadorEventos, ILogger<EventosController> logger)
    {
        _publicadorEventos = publicadorEventos;
        _logger = logger;
    }

    /// <summary>
    /// Publica um evento no sistema
    /// </summary>
    [HttpPost("publicar")]
    public async Task<IActionResult> PublicarEvento([FromBody] RequestPublicacaoEvento request)
    {
        await _publicadorEventos.PublicarAsync(request.Topico, request.TipoEvento, request.DadosEvento);
        return Ok(new { Mensagem = "Evento publicado com sucesso" });
    }

    /// <summary>
    /// Publica evento de cotação (simulação de consumer)
    /// </summary>
    [HttpPost("cotacao")]
    public async Task<IActionResult> PublicarEventoCotacao([FromBody] EventoCotacao evento)
    {
        await _publicadorEventos.PublicarAsync("topico.cotacoes", "CotacaoRecebida", evento);
        return Ok(new { Mensagem = "Evento de cotação publicado" });
    }

    /// <summary>
    /// Publica evento de MTM (simulação de consumer)
    /// </summary>
    [HttpPost("mtm")]
    public async Task<IActionResult> PublicarEventoMtm([FromBody] EventoMtm evento)
    {
        await _publicadorEventos.PublicarAsync("topico.mtm", "MtmCalculado", evento);
        return Ok(new { Mensagem = "Evento de MTM publicado" });
    }

    /// <summary>
    /// Publica evento de barreira (simulação de consumer)
    /// </summary>
    [HttpPost("barreira")]
    public async Task<IActionResult> PublicarEventoBarreira([FromBody] EventoBarreira evento)
    {
        await _publicadorEventos.PublicarAsync("topico.barreiras", "BarreiraObservada", evento);
        return Ok(new { Mensagem = "Evento de barreira publicado" });
    }

    /// <summary>
    /// Publica evento corporativo (simulação de consumer)
    /// </summary>
    [HttpPost("evento-corporativo")]
    public async Task<IActionResult> PublicarEventoCorporativo([FromBody] EventoCorporativoData evento)
    {
        await _publicadorEventos.PublicarAsync("topico.eventos-corporativos", "EventoCorporativoDetectado", evento);
        return Ok(new { Mensagem = "Evento corporativo publicado" });
    }
}

public record RequestPublicacaoEvento(string Topico, string TipoEvento, object DadosEvento);
public record EventoCotacao(string CodigoAtivo, decimal PrecoFechamento, DateTime DataReferencia);
public record EventoMtm(Guid CoeId, DateTime DataReferencia);
public record EventoBarreira(Guid BarreiraId, Guid CoeId, string TipoBarreira, DateTime DataObservacao);
public record EventoCorporativoData(Guid EventoId, string TipoEvento, string CodigoAtivo, decimal? FatorAjuste);

