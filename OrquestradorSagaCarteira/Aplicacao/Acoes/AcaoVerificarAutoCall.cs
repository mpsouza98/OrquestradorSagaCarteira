using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se autocall foi atingido para operações
/// Regra: Autocall é atingido quando TODAS as barreiras (não-Autocall) de uma operação estão atingidas
/// </summary>
public class AcaoVerificarAutoCall : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly IPublicadorEventos _publicador;
    private readonly ILogger<AcaoVerificarAutoCall> _logger;

    public AcaoVerificarAutoCall(
        IBarreiraRepository barreiraRepository,
        IPublicadorEventos publicador,
        ILogger<AcaoVerificarAutoCall> logger)
    {
        _barreiraRepository = barreiraRepository;
        _publicador = publicador;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            // 1) Ler do contexto persistido da saga
            if (string.IsNullOrEmpty(etapa.Saga.DadosContexto))
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Contexto da saga não disponível" };

            var contexto = JsonSerializer.Deserialize<ContextoComBarreiras>(etapa.Saga.DadosContexto) ?? new();
            var operacaoIds = contexto.Barreiras
                .Select(b => b.OperacaoId)
                .Distinct()
                .ToList();

            if (!operacaoIds.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma operação para verificar autocall");
                return new ResultadoAcao { Sucesso = true, DadosSaida = JsonSerializer.Serialize(new { OperacoesNotificadas = new List<Guid>() }) };
            }

            _logger.LogInformation("🔍 Verificando autocall para {Qtd} operações (consulta única)", operacaoIds.Count);

            // 2) Consultar todas as barreiras das operações em uma única query
            var barreiras = await _barreiraRepository.ObterPorOperacoesAsync(operacaoIds);

            // 3) Agrupar por operação (operacaoId => lista de barreiras)
            var porOperacao = barreiras
                .GroupBy(b => b.OperacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 4) Filtrar operações cuja cesta (todas barreiras não-Autocall e ativas) está totalmente atingida
            var operacoesElegiveis = porOperacao
                .Where(kv => kv.Value
                    .Where(b => b.Ativa && b.TipoBarreira != "Autocall")
                    .All(b => b.Atingida))
                .Select(kv => new
                {
                    OperacaoId = kv.Key,
                    BarreiraAutocall = kv.Value.FirstOrDefault(b => b.TipoBarreira == "Autocall" && b.Ativa)
                })
                .Where(x => x.BarreiraAutocall != null)
                .ToList();

            if (!operacoesElegiveis.Any())
            {
                _logger.LogInformation("➖ Nenhuma operação elegível para autocall neste ciclo");
                return new ResultadoAcao { Sucesso = true, DadosSaida = JsonSerializer.Serialize(new { OperacoesNotificadas = new List<Guid>() }) };
            }

            // 5) Notificar evento de autocall para cada operação elegível
            var notificadas = new List<Guid>();
            foreach (var op in operacoesElegiveis)
            {
                var mensagem = JsonSerializer.Serialize(new
                {
                    OperacaoId = op.OperacaoId,
                    BarreiraAutocallId = op.BarreiraAutocall!.Id,
                    AutocallAtingido = true,
                    TipoEvento = "AutocallVerificado",
                    DataEvento = DateTime.UtcNow
                });

                await _publicador.PublicarAsync("topico.autocall", mensagem);
                notificadas.Add(op.OperacaoId);

                _logger.LogInformation("✉️ Autocall notificado - OperacaoId: {OperacaoId}, BarreiraAutocallId: {BarreiraId}",
                    op.OperacaoId, op.BarreiraAutocall!.Id);
            }

            var dadosSaida = JsonSerializer.Serialize(new
            {
                TotalOperacoes = operacaoIds.Count,
                OperacoesNotificadas = notificadas
            });

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao verificar autocall na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Notificação publicada não requer compensação neste fluxo
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

// Modelos auxiliares para desserializar o contexto
class ContextoComBarreiras
{
    public List<BarreiraCtx> Barreiras { get; set; } = new();
}

class BarreiraCtx
{
    public Guid BarreiraId { get; set; }
    public Guid OperacaoId { get; set; }
    public decimal NivelBarreira { get; set; }
    public string Condicao { get; set; } = string.Empty;
}
