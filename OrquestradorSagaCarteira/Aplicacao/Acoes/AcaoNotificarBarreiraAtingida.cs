using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para notificar barreira atingida (publica no tópico)
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
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            var mensagem = JsonSerializer.Serialize(new
            {
                EventoId = dados.EventoId,
                OperacaoId = dados.OperacaoId,
                TipoEvento = "BarreiraAtingida",
                DataEvento = DateTime.UtcNow
            });

            await _publicador.PublicarAsync("topico.barreira", mensagem);

            _logger.LogInformation(
                "Barreira notificada no tópico - EventoId: {EventoId}, OperacaoId: {OperacaoId}",
                dados.EventoId, dados.OperacaoId);

            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar barreira na etapa {EtapaId}", etapa.Id);
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
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
}

