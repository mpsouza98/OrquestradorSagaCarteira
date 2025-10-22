using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para notificar barreiras atingidas em lote (publica no tópico)
/// </summary>
public class AcaoNotificarBarreiraAtingida : IAcaoSaga
{
    private readonly IPublicadorEventos _publicador;
    private readonly ILogger<AcaoNotificarBarreiraAtingida> _logger;

    public AcaoNotificarBarreiraAtingida(
        IPublicadorEventos publicador,
        ILogger<AcaoNotificarBarreiraAtingida> logger)
    {
        _publicador = publicador;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosNotificarBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || dados.Eventos == null || !dados.Eventos.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma barreira para notificar");
                return new ResultadoAcao { Sucesso = true };
            }

            // Construir mensagem com lista de OperacoesIds (distintas)
            var operacoesIds = dados.Eventos
                .Select(e => e.OperacaoId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!operacoesIds.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma operação válida encontrada para notificação de autocall");
                return new ResultadoAcao { Sucesso = true };
            }

            var mensagem = JsonSerializer.Serialize(new
            {
                OperacoesIds = operacoesIds,
                TipoEvento = "BarreirasAtingidas",
                DataEvento = DateTime.UtcNow
            });

            await _publicador.PublicarAsync("topico.barreira", mensagem);

            _logger.LogInformation(
                "📢 Notificação enviada - {Qtd} operações publicadas no tópico 'topico.barreira'",
                operacoesIds.Count);

            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao notificar barreiras na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Notificação não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

public class DadosNotificarBarreira
{
    public List<EventoNotificacao> Eventos { get; set; } = new();
}

public class EventoNotificacao
{
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
    public string Ticker { get; set; } = string.Empty;
}
