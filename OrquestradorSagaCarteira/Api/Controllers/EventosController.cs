using Microsoft.AspNetCore.Mvc;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/eventos")]
public class EventosController : ControllerBase
{
    private readonly ICotacaoRepository _cotacaoRepository;
    private readonly IPublicadorEventos _publicador;
    private readonly ILogger<EventosController> _logger;

    public EventosController(
        ICotacaoRepository cotacaoRepository,
        IPublicadorEventos publicador,
        ILogger<EventosController> logger)
    {
        _cotacaoRepository = cotacaoRepository;
        _publicador = publicador;
        _logger = logger;
    }

    /// <summary>
    /// Publica um evento de cotação no sistema
    /// </summary>
    [HttpPost("cotacoes")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PublicarCotacao([FromBody] PublicarCotacaoRequest request)
    {
        try
        {
            // Persistir cotação
            var cotacao = new Cotacao
            {
                Id = Guid.NewGuid(),
                TickerAtivo = request.Ticker,
                Fonte = request.Fonte,
                Data = request.Data.Date,
                PrecoFechamento = request.PrecoFechamento,
                DataCriacao = DateTime.UtcNow
            };

            await _cotacaoRepository.InserirAsync(cotacao);

            // Publicar evento no tópico
            var evento = new
            {
                request.Ticker,
                request.Data,
                request.PrecoFechamento,
                request.Fonte
            };

            await _publicador.PublicarAsync("topico.cotacao", JsonSerializer.Serialize(evento));

            _logger.LogInformation("📊 Cotação publicada - Ticker: {Ticker}, Preço: {Preco}, Data: {Data}",
                request.Ticker, request.PrecoFechamento, request.Data);

            return Accepted(new
            {
                Mensagem = "Cotação publicada com sucesso",
                CotacaoId = cotacao.Id,
                Ticker = request.Ticker
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar cotação");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Publica múltiplas cotações em lote
    /// </summary>
    [HttpPost("cotacoes/lote")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PublicarCotacoesLote([FromBody] List<PublicarCotacaoRequest> requests)
    {
        try
        {
            var processadas = 0;

            foreach (var request in requests)
            {
                var cotacao = new Cotacao
                {
                    Id = Guid.NewGuid(),
                    TickerAtivo = request.Ticker,
                    Fonte = request.Fonte,
                    Data = request.Data.Date,
                    PrecoFechamento = request.PrecoFechamento,
                    DataCriacao = DateTime.UtcNow
                };

                await _cotacaoRepository.InserirAsync(cotacao);

                var evento = new
                {
                    request.Ticker,
                    request.Data,
                    request.PrecoFechamento,
                    request.Fonte
                };

                await _publicador.PublicarAsync("topico.cotacao", JsonSerializer.Serialize(evento));
                processadas++;
            }

            _logger.LogInformation("📊 Lote de cotações publicado - Total: {Total}", processadas);

            return Accepted(new
            {
                Mensagem = $"{processadas} cotações publicadas com sucesso",
                TotalProcessado = processadas
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar lote de cotações");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Obtém cotação de um ativo em uma data específica
    /// </summary>
    [HttpGet("cotacoes/{ticker}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCotacao(string ticker, [FromQuery] DateTime data)
    {
        try
        {
            var cotacao = await _cotacaoRepository.ObterCotacaoAsync(ticker, data);
            
            if (cotacao == null)
                return NotFound(new { Mensagem = "Cotação não encontrada" });

            return Ok(new
            {
                cotacao.Id,
                cotacao.TickerAtivo,
                cotacao.Data,
                cotacao.PrecoFechamento,
                cotacao.Fonte,
                cotacao.DataCriacao
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter cotação");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }
}

public class PublicarCotacaoRequest
{
    public string Ticker { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public decimal PrecoFechamento { get; set; }
    public string Fonte { get; set; } = "API";
}

