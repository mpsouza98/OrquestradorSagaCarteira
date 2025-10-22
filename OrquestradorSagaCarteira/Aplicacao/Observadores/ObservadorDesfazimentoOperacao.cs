using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador de eventos de autocall que dispara liquidação agendada
/// </summary>
public class ObservadorDesfazimentoOperacao : IObservador
{
    private readonly IOrquestradorSaga _orquestrador;
    private readonly ILogger<ObservadorDesfazimentoOperacao> _logger;

    public string NomeObservador => "ObservadorDesfazimentoOperacao";

    public ObservadorDesfazimentoOperacao(
        IOrquestradorSaga orquestrador,
        ILogger<ObservadorDesfazimentoOperacao> logger)
    {
        _orquestrador = orquestrador;
        _logger = logger;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("ObservadorDesfazimentoOperacao recebeu evento do tópico {Topico}", topico);

            var evento = JsonSerializer.Deserialize<EventoAutocallVerificado>(dadosEvento);
            if (evento == null || !evento.AutocallAtingido) return;

            _logger.LogInformation(
                "Iniciando saga para liquidação agendada - OperacaoId: {OperacaoId}",
                evento.OperacaoId);

            // Criar saga para processar liquidação
            var saga = new Saga
            {
                Id = Guid.NewGuid(),
                TipoSaga = TipoSaga.ProcessamentoAutocall,
                EstadoSaga = EstadoSaga.Iniciada,
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow,
                DadosContexto = JsonSerializer.Serialize(new { evento.OperacaoId, evento.EventoId })
            };

            // Etapa 1: Agendar Desfazimento de Operação (Liquidação)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Agendar Desfazimento Operação",
                OrdemExecucao = 1,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.AgendarDesfazimentoOperacao,
                DadosEntrada = JsonSerializer.Serialize(new
                {
                    OperacaoId = evento.OperacaoId,
                    AutocallAtingido = evento.AutocallAtingido,
                    ValorCesta = 0m, // Será obtido do contexto
                    DataLiquidacao = DateTime.UtcNow.AddDays(2) // D+2
                })
            });

            await _orquestrador.IniciarSagaAsync(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no ObservadorDesfazimentoOperacao ao processar evento");
        }
    }
}

public class EventoAutocallVerificado
{
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
    public bool AutocallAtingido { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
}

