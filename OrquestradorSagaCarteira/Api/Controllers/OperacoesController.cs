using Microsoft.AspNetCore.Mvc;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/sagas/operacoes")]
public class OperacoesController : ControllerBase
{
    private readonly IOperacaoRepository _operacaoRepository;
    private readonly ILogger<OperacoesController> _logger;

    public OperacoesController(
        IOperacaoRepository operacaoRepository,
        ILogger<OperacoesController> logger)
    {
        _operacaoRepository = operacaoRepository;
        _logger = logger;
    }

    /// <summary>
    /// Cria uma nova operação (instância dentro da saga)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CriarOperacao([FromBody] CriarOperacaoRequest request)
    {
        try
        {
            var operacao = new Operacao
            {
                Id = Guid.NewGuid(),
                CodigoOperacao = request.CodigoOperacao,
                Descricao = request.Descricao,
                DataCriacao = DateTime.UtcNow,
                DataVencimento = request.DataVencimento,
                ValorNominal = request.ValorNominal,
                TipoEstrutura = request.TipoEstrutura,
                Ativa = true,
                DataAtualizacao = DateTime.UtcNow
            };

            // Adicionar ativos
            foreach (var ativo in request.Ativos)
            {
                operacao.Ativos.Add(new AtivoOperacao
                {
                    Id = Guid.NewGuid(),
                    OperacaoId = operacao.Id,
                    Ticker = ativo.Ticker,
                    CotacaoInicial = ativo.CotacaoInicial,
                    PercentualParticipacao = ativo.PercentualParticipacao,
                    DataCriacao = DateTime.UtcNow
                });
            }

            // Adicionar barreiras
            foreach (var barreira in request.Barreiras)
            {
                operacao.Barreiras.Add(new BarreiraOperacao
                {
                    Id = Guid.NewGuid(),
                    OperacaoId = operacao.Id,
                    Ticker = barreira.Ticker,
                    TipoBarreira = barreira.TipoBarreira,
                    Condicao = barreira.Condicao,
                    NivelBarreira = barreira.NivelBarreira,
                    DataObservacao = barreira.DataObservacao,
                    Atingida = false,
                    Ativa = true,
                    DataCriacao = DateTime.UtcNow
                });
            }

            await _operacaoRepository.InserirAsync(operacao);

            _logger.LogInformation("✅ Operação criada: {CodigoOperacao}", operacao.CodigoOperacao);

            return CreatedAtAction(
                nameof(ObterOperacao),
                new { id = operacao.Id },
                new
                {
                    operacao.Id,
                    operacao.CodigoOperacao,
                    operacao.Descricao,
                    operacao.DataCriacao,
                    TotalAtivos = operacao.Ativos.Count,
                    TotalBarreiras = operacao.Barreiras.Count
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar operação");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Obtém uma operação por ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterOperacao(Guid id)
    {
        try
        {
            var operacao = await _operacaoRepository.ObterPorIdAsync(id);
            
            if (operacao == null)
                return NotFound(new { Mensagem = "Operação não encontrada" });

            return Ok(new
            {
                operacao.Id,
                operacao.CodigoOperacao,
                operacao.Descricao,
                operacao.DataCriacao,
                operacao.DataVencimento,
                operacao.ValorNominal,
                operacao.TipoEstrutura,
                operacao.Ativa,
                Ativos = operacao.Ativos,
                Barreiras = operacao.Barreiras
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter operação {OperacaoId}", id);
            return StatusCode(500, new { Erro = ex.Message });
        }
    }

    /// <summary>
    /// Lista todas as operações ativas
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarOperacoes()
    {
        try
        {
            var operacoes = await _operacaoRepository.ListarAtivasAsync();
            
            return Ok(operacoes.Select(o => new
            {
                o.Id,
                o.CodigoOperacao,
                o.Descricao,
                o.DataCriacao,
                o.DataVencimento,
                o.ValorNominal,
                o.TipoEstrutura,
                o.Ativa
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar operações");
            return StatusCode(500, new { Erro = ex.Message });
        }
    }
}

public class CriarOperacaoRequest
{
    public string CodigoOperacao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public DateTime DataVencimento { get; set; }
    public decimal ValorNominal { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty; // BestOf, WorstOf
    public List<AtivoRequest> Ativos { get; set; } = new();
    public List<BarreiraRequest> Barreiras { get; set; } = new();
}

public class AtivoRequest
{
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoInicial { get; set; }
    public decimal PercentualParticipacao { get; set; }
}

public class BarreiraRequest
{
    public string? Ticker { get; set; }
    public string TipoBarreira { get; set; } = string.Empty;
    public string Condicao { get; set; } = string.Empty;
    public decimal NivelBarreira { get; set; }
    public DateTime DataObservacao { get; set; }
}

