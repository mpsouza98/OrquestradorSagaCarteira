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
            var json = etapa.DadosEntrada ?? "[]";

            // Tentar 1: Lista direta de eventos criados
            List<EventoAutocallCriado>? eventos = null;
            try
            {
                eventos = JsonSerializer.Deserialize<List<EventoAutocallCriado>>(json);
            }
            catch { /* Ignorar e tentar outros formatos */ }

            // Tentar 2: Wrapper { EventosCriados: [...] }
            if (eventos == null)
            {
                try
                {
                    var wrapper = JsonSerializer.Deserialize<EventosCriadosWrapper>(json);
                    if (wrapper?.EventosCriados != null)
                        eventos = wrapper.EventosCriados;
                }
                catch { /* Ignorar e tentar legado */ }
            }

            // Tentar 3: DTO legado (single)
            if (eventos == null)
            {
                try
                {
                    var legado = JsonSerializer.Deserialize<DadosNotificarAutocall>(json);
                    if (legado != null)
                    {
                        eventos = new List<EventoAutocallCriado>
                        {
                            new EventoAutocallCriado
                            {
                                EventoId = legado.EventoId,
                                OperacaoId = legado.OperacaoId,
                                BarreiraAutocallId = Guid.Empty
                            }
                        };
                    }
                }
                catch
                {
                    // Se falhar todos os formatos, lançar erro mais claro
                }
            }

            if (eventos == null || eventos.Count == 0)
            {
                _logger.LogInformation("ℹ️ Nenhum evento de autocall para notificar");
                return new ResultadoAcao { Sucesso = true };
            }

            var publicados = 0;
            foreach (var e in eventos)
            {
                var mensagem = JsonSerializer.Serialize(new
                {
                    e.EventoId,
                    e.OperacaoId,
                    e.BarreiraAutocallId,
                    AutocallAtingido = true,
                    TipoEvento = "AutocallVerificado",
                    DataEvento = DateTime.UtcNow
                });

                await _publicador.PublicarAsync("topico.autocall", mensagem);
                publicados++;
            }

            _logger.LogInformation("📢 Notificação de autocall publicada para {Qtd} eventos", publicados);
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

public class EventosCriadosWrapper
{
    public List<EventoAutocallCriado> EventosCriados { get; set; } = new();
}
