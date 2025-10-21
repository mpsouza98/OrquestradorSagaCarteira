using Microsoft.AspNetCore.Mvc;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SagaController : ControllerBase
{
    private readonly IOrquestradorSaga _orquestradorSaga;
    private readonly ILogger<SagaController> _logger;

    public SagaController(IOrquestradorSaga orquestradorSaga, ILogger<SagaController> logger)
    {
        _orquestradorSaga = orquestradorSaga;
        _logger = logger;
    }

    /// <summary>
    /// Inicia uma nova saga de valorização MTM
    /// </summary>
    [HttpPost("valorizacao-mtm")]
    public async Task<ActionResult<Saga>> IniciarSagaValorizacaoMtm([FromBody] RequestValorizacaoMtm request)
    {
        var saga = new Saga
        {
            TipoSaga = TipoSaga.ValorizacaoMtm,
            DadosContexto = JsonSerializer.Serialize(request),
            Etapas = new List<EtapaSaga>
            {
                new EtapaSaga
                {
                    NomeEtapa = "Calcular MTM Renda Fixa",
                    TipoAcao = TipoAcao.CalcularMtmRendaFixa,
                    DadosEntrada = JsonSerializer.Serialize(new { request.CoeId, request.DataReferencia })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Calcular MTM Renda Variável",
                    TipoAcao = TipoAcao.CalcularMtmRendaVariavel,
                    DadosEntrada = JsonSerializer.Serialize(new { request.CoeId, request.DataReferencia })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Consolidar MTM",
                    TipoAcao = TipoAcao.ConsolidarMtm,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        request.CoeId, 
                        request.DataReferencia,
                        ValorRendaFixa = 0m,
                        ValorRendaVariavel = 0m
                    })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Calcular Valorização Contábil",
                    TipoAcao = TipoAcao.CalcularValorizacaoContabil,
                    DadosEntrada = JsonSerializer.Serialize(new { request.CoeId, request.DataReferencia })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Atualizar Posição Cliente",
                    TipoAcao = TipoAcao.AtualizarPosicaoCliente,
                    DadosEntrada = JsonSerializer.Serialize(new { request.CoeId, request.DataReferencia })
                }
            }
        };

        var resultado = await _orquestradorSaga.IniciarSagaAsync(saga);
        return CreatedAtAction(nameof(ObterSaga), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Inicia uma nova saga de autocall
    /// </summary>
    [HttpPost("autocall")]
    public async Task<ActionResult<Saga>> IniciarSagaAutocall([FromBody] RequestAutocall request)
    {
        var saga = new Saga
        {
            TipoSaga = TipoSaga.AtivacaoAutocall,
            DadosContexto = JsonSerializer.Serialize(request),
            Etapas = new List<EtapaSaga>
            {
                new EtapaSaga
                {
                    NomeEtapa = "Verificar Barreira",
                    TipoAcao = TipoAcao.VerificarBarreira,
                    DadosEntrada = JsonSerializer.Serialize(new { request.BarreiraId })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Processar Atingimento",
                    TipoAcao = TipoAcao.ProcessarAtingimentoBarreira,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        request.BarreiraId,
                        BarreiraAtingida = false,
                        ValorCesta = 0m
                    })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Iniciar Liquidação",
                    TipoAcao = TipoAcao.IniciarLiquidacao,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        request.CoeId,
                        request.BarreiraId,
                        request.DataLiquidacao
                    })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Calcular Valor Liquidação",
                    TipoAcao = TipoAcao.CalcularValorLiquidacao,
                    DadosEntrada = JsonSerializer.Serialize(new { LiquidacaoId = Guid.Empty })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Liquidar Posições",
                    TipoAcao = TipoAcao.LiquidarPosicoes,
                    DadosEntrada = JsonSerializer.Serialize(new { LiquidacaoId = Guid.Empty })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Encerrar COE",
                    TipoAcao = TipoAcao.EncerrarCoe,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        request.CoeId,
                        LiquidacaoId = Guid.Empty
                    })
                }
            }
        };

        var resultado = await _orquestradorSaga.IniciarSagaAsync(saga);
        return CreatedAtAction(nameof(ObterSaga), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Inicia uma saga de processamento de evento corporativo
    /// </summary>
    [HttpPost("evento-corporativo")]
    public async Task<ActionResult<Saga>> IniciarSagaEventoCorporativo([FromBody] RequestEventoCorporativo request)
    {
        TipoAcao tipoAcao = request.TipoEvento switch
        {
            "Split" => TipoAcao.ProcessarSplit,
            "Inplit" => TipoAcao.ProcessarInsplit,
            _ => TipoAcao.AjustarPosicoes
        };

        var saga = new Saga
        {
            TipoSaga = TipoSaga.ProcessamentoEventoCorporativo,
            DadosContexto = JsonSerializer.Serialize(request),
            Etapas = new List<EtapaSaga>
            {
                new EtapaSaga
                {
                    NomeEtapa = $"Processar {request.TipoEvento}",
                    TipoAcao = tipoAcao,
                    DadosEntrada = JsonSerializer.Serialize(new { request.EventoId })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Ajustar Posições",
                    TipoAcao = TipoAcao.AjustarPosicoes,
                    DadosEntrada = JsonSerializer.Serialize(new { request.EventoId })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Realizar Ajuste Contábil",
                    TipoAcao = TipoAcao.RealizarAjusteContabil,
                    DadosEntrada = JsonSerializer.Serialize(new { request.EventoId })
                }
            }
        };

        var resultado = await _orquestradorSaga.IniciarSagaAsync(saga);
        return CreatedAtAction(nameof(ObterSaga), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Inicia uma saga de processamento de cotação
    /// </summary>
    [HttpPost("processar-cotacao")]
    public async Task<ActionResult<Saga>> ProcessarCotacao([FromBody] RequestCotacao request)
    {
        var saga = new Saga
        {
            TipoSaga = TipoSaga.ProcessamentoCotacao,
            DadosContexto = JsonSerializer.Serialize(request),
            Etapas = new List<EtapaSaga>
            {
                new EtapaSaga
                {
                    NomeEtapa = "Processar Cotação",
                    TipoAcao = TipoAcao.ProcessarCotacao,
                    DadosEntrada = JsonSerializer.Serialize(request)
                }
            }
        };

        var resultado = await _orquestradorSaga.IniciarSagaAsync(saga);
        return CreatedAtAction(nameof(ObterSaga), new { id = resultado.Id }, resultado);
    }

    /// <summary>
    /// Obtém uma saga por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Saga>> ObterSaga(Guid id)
    {
        var saga = await _orquestradorSaga.ObterSagaAsync(id);
        if (saga == null)
            return NotFound();

        return Ok(saga);
    }

    /// <summary>
    /// Lista todas as sagas com paginação
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Saga>>> ListarSagas([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50)
    {
        var sagas = await _orquestradorSaga.ListarSagasAsync(pagina, tamanhoPagina);
        return Ok(sagas);
    }

    /// <summary>
    /// Força a compensação de uma saga
    /// </summary>
    [HttpPost("{id}/compensar")]
    public async Task<IActionResult> CompensarSaga(Guid id)
    {
        await _orquestradorSaga.CompensarSagaAsync(id);
        return Ok(new { Mensagem = "Compensação iniciada" });
    }

    /// <summary>
    /// Executa a próxima etapa de uma saga (útil para debugging)
    /// </summary>
    [HttpPost("{id}/executar-proxima-etapa")]
    public async Task<IActionResult> ExecutarProximaEtapa(Guid id)
    {
        await _orquestradorSaga.ExecutarProximaEtapaAsync(id);
        return Ok(new { Mensagem = "Execução da próxima etapa iniciada" });
    }
}

public record RequestValorizacaoMtm(Guid CoeId, DateTime DataReferencia);
public record RequestAutocall(Guid CoeId, Guid BarreiraId, DateTime DataLiquidacao);
public record RequestEventoCorporativo(Guid EventoId, string TipoEvento);
public record RequestCotacao(
    string CodigoAtivo, 
    string TipoAtivo, 
    DateTime DataReferencia, 
    decimal? PrecoAbertura, 
    decimal? PrecoFechamento, 
    decimal? PrecoMaximo, 
    decimal? PrecoMinimo, 
    decimal? Volume
);

