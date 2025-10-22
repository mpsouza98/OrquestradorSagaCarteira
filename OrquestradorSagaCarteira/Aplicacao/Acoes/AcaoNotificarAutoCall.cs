using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para notificar autocall (publica no tópico)
/// </summary>
public class AcaoNotificarAutoCall : IAcaoSaga
{
    private readonly IPublicadorEventos _publicador;
    private readonly ILogger<AcaoNotificarAutoCall> _logger;

    public AcaoNotificarAutoCall(
        IPublicadorEventos publicador,
        ILogger<AcaoNotificarAutoCall> logger)
    {
        _publicador = publicador;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosNotificarAutocall>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            var mensagem = JsonSerializer.Serialize(new
            {
                EventoId = dados.EventoId,
                OperacaoId = dados.OperacaoId,
                AutocallAtingido = dados.AutocallAtingido,
                TipoEvento = "AutocallVerificado",
                DataEvento = DateTime.UtcNow
            });

            await _publicador.PublicarAsync("topico.autocall", mensagem);

            _logger.LogInformation(
                "Autocall notificado no tópico - EventoId: {EventoId}, Atingido: {Atingido}",
                dados.EventoId, dados.AutocallAtingido);

            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar autocall na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Notificação não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

public class DadosNotificarAutocall
{
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
    public bool AutocallAtingido { get; set; }
}

