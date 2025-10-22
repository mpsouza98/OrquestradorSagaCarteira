using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se autocall foi atingido para operações
/// Regra: Autocall é atingido quando TODAS as barreiras de uma operação estão atingidas
/// </summary>
public class AcaoVerificarAutoCall : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly IOperacaoRepository _operacaoRepository;
    private readonly ILogger<AcaoVerificarAutoCall> _logger;

    public AcaoVerificarAutoCall(
        IBarreiraRepository barreiraRepository,
        IOperacaoRepository operacaoRepository,
        ILogger<AcaoVerificarAutoCall> logger)
    {
        _barreiraRepository = barreiraRepository;
        _operacaoRepository = operacaoRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            // Obter dados do contexto da saga
            if (etapa.Saga == null || string.IsNullOrEmpty(etapa.Saga.DadosContexto))
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Contexto da saga não disponível" };

            var contexto = JsonSerializer.Deserialize<ContextoBarreira>(etapa.Saga.DadosContexto);
            if (contexto == null || contexto.Barreiras == null || !contexto.Barreiras.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma barreira para verificar autocall no contexto");
                return new ResultadoAcao { Sucesso = true, DadosSaida = JsonSerializer.Serialize(new
                {
                    TotalOperacoes = 0,
                    OperacoesComAutocall = new List<AutocallAtingido>()
                })};
            }

            // Extrair lista de OperacaoId únicas das barreiras
            var operacaoIds = contexto.Barreiras.Select(b => b.OperacaoId).Distinct().ToList();

            _logger.LogInformation("🔍 Verificando autocall para {Qtd} operações", operacaoIds.Count);

            var operacoesComAutocall = new List<AutocallAtingido>();

            // Buscar todas as barreiras em lote para as operações
            var todasBarreiras = await _barreiraRepository.ObterPorOperacoesAsync(operacaoIds);
            var porOperacao = todasBarreiras.GroupBy(b => b.OperacaoId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var operacaoId in operacaoIds)
            {
                if (!porOperacao.TryGetValue(operacaoId, out var barreirasOp) || !barreirasOp.Any())
                    continue;

                var barreirasAtivasNaoAutocall = barreirasOp.Where(b => b.Ativa && b.TipoBarreira != "Autocall").ToList();
                if (!barreirasAtivasNaoAutocall.Any())
                    continue;

                var todasAtingidas = barreirasAtivasNaoAutocall.All(b => b.Atingida);
                var totalBarreiras = barreirasAtivasNaoAutocall.Count;
                var barreirasAtingidas = barreirasAtivasNaoAutocall.Count(b => b.Atingida);

                _logger.LogInformation("📊 Operação {OperacaoId} - {Atingidas}/{Total} barreiras atingidas - Autocall: {Autocall}",
                    operacaoId, barreirasAtingidas, totalBarreiras, todasAtingidas);

                if (!todasAtingidas) continue;

                var barreiraAutocall = barreirasOp.FirstOrDefault(b => b.TipoBarreira == "Autocall" && b.Ativa);
                if (barreiraAutocall == null) continue;

                var operacao = await _operacaoRepository.ObterPorIdAsync(operacaoId);
                if (operacao == null) continue;

                operacoesComAutocall.Add(new AutocallAtingido
                {
                    OperacaoId = operacaoId,
                    BarreiraAutocallId = barreiraAutocall.Id,
                    NivelBarreira = barreiraAutocall.NivelBarreira,
                    TotalBarreiras = totalBarreiras,
                    BarreirasAtingidas = barreirasAtingidas,
                    TipoEstrutura = operacao.TipoEstrutura,
                    DataVencimento = operacao.DataVencimento
                });
            }

            var saida = JsonSerializer.Serialize(new
            {
                TotalOperacoes = operacaoIds.Count,
                OperacoesComAutocall = operacoesComAutocall
            });

            return new ResultadoAcao { Sucesso = true, DadosSaida = saida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao verificar autocall na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}
public class AutocallAtingido
{
    public Guid OperacaoId { get; set; }
    public Guid BarreiraAutocallId { get; set; }
    public decimal NivelBarreira { get; set; }
    public int TotalBarreiras { get; set; }
    public int BarreirasAtingidas { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty;
    public DateTime DataVencimento { get; set; }
}
