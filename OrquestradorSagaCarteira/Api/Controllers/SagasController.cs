using Microsoft.AspNetCore.Mvc;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Aplicacao.Observadores;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SagasController : ControllerBase
{
    private readonly IOrquestradorSaga _orquestrador;
    private readonly IPublicadorEventos _publicador;
    private readonly ObservadorBarreira _observadorBarreira;
    private readonly ObservadorAutoCall _observadorAutoCall;
    private readonly ObservadorDesfazimentoOperacao _observadorDesfazimento;
    private readonly ILogger<SagasController> _logger;

    public SagasController(
        IOrquestradorSaga orquestrador,
        IPublicadorEventos publicador,
        ObservadorBarreira observadorBarreira,
        ObservadorAutoCall observadorAutoCall,
        ObservadorDesfazimentoOperacao observadorDesfazimento,
        ILogger<SagasController> logger)
    {
        _orquestrador = orquestrador;
        _publicador = publicador;
        _observadorBarreira = observadorBarreira;
        _observadorAutoCall = observadorAutoCall;
        _observadorDesfazimento = observadorDesfazimento;
        _logger = logger;
    }

    /// <summary>
    /// Inicializa os observadores e estrutura da saga
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InicializarSaga()
    {
        try
        {
            // Inscrever observadores nos tópicos
            await _publicador.InscreverObservadorAsync("topico.cotacao", _observadorBarreira);
            await _publicador.InscreverObservadorAsync("topico.barreira", _observadorAutoCall);
            await _publicador.InscreverObservadorAsync("topico.autocall", _observadorDesfazimento);

            _logger.LogInformation("🚀 Sistema de saga Autocall inicializado com sucesso");

            return Ok(new
            {
                Mensagem = "Sistema de saga Autocall inicializado",
                Observadores = new[]
                {
                    new { Nome = _observadorBarreira.NomeObservador, Topico = "topico.cotacao" },
                    new { Nome = _observadorAutoCall.NomeObservador, Topico = "topico.barreira" },
                    new { Nome = _observadorDesfazimento.NomeObservador, Topico = "topico.autocall" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar saga");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Lista todas as sagas
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarSagas([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50)
    {
        try
        {
            var sagas = await _orquestrador.ListarSagasAsync(pagina, tamanhoPagina);
            
            return Ok(sagas.Select(s => new
            {
                s.Id,
                s.TipoSaga,
                s.EstadoSaga,
                s.DataCriacao,
                s.DataAtualizacao,
                s.DataFinalizacao,
                TotalEtapas = s.Etapas.Count,
                EtapasConcluidas = s.Etapas.Count(e => e.EstadoEtapa == Dominio.Enums.EstadoEtapa.Concluida)
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar sagas");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Obtém detalhes de uma saga específica
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterSaga(Guid id)
    {
        try
        {
            var saga = await _orquestrador.ObterSagaAsync(id);
            
            if (saga == null)
                return NotFound(new { Mensagem = "Saga não encontrada" });

            return Ok(new
            {
                saga.Id,
                saga.TipoSaga,
                saga.EstadoSaga,
                saga.DataCriacao,
                saga.DataAtualizacao,
                saga.DataFinalizacao,
                saga.DadosContexto,
                saga.MensagemErro,
                Etapas = saga.Etapas.Select(e => new
                {
                    e.Id,
                    e.NomeEtapa,
                    e.OrdemExecucao,
                    e.EstadoEtapa,
                    e.TipoAcao,
                    e.DataInicio,
                    e.DataFinalizacao,
                    e.MensagemErro,
                    e.Tentativas
                }).OrderBy(e => e.OrdemExecucao)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter saga {SagaId}", id);
            return StatusCode(500, new { Erro = ex.Message });
        }
    }
}

