using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador de eventos de barreira que dispara verificação de autocall
/// </summary>
public class ObservadorAutoCall : IObservador
{
    private readonly IOrquestradorSaga _orquestrador;
    private readonly ILogger<ObservadorAutoCall> _logger;

    public string NomeObservador => "ObservadorAutoCall";

    public ObservadorAutoCall(
        IOrquestradorSaga orquestrador,
        ILogger<ObservadorAutoCall> logger)
    {
        _orquestrador = orquestrador;
        _logger = logger;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("ObservadorAutoCall recebeu evento do tópico {Topico}", topico);

            var evento = JsonSerializer.Deserialize<EventoBarreiraAtingida>(dadosEvento);
            if (evento == null) return;

            _logger.LogInformation(
                "Iniciando saga para verificação de autocall - OperacaoId: {OperacaoId}",
                evento.OperacaoId);

            // Criar saga para processar autocall
            var saga = new Saga
            {
                Id = Guid.NewGuid(),
                TipoSaga = TipoSaga.ProcessamentoAutocall,
                EstadoSaga = EstadoSaga.Iniciada,
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow,
                DadosContexto = JsonSerializer.Serialize(new { evento.OperacaoId, evento.EventoId })
            };

            // Etapa 1: Agregar Cesta
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Agregar Cesta",
                OrdemExecucao = 1,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.AgregarCesta,
                DadosEntrada = JsonSerializer.Serialize(new
                {
                    OperacaoId = evento.OperacaoId,
                    DataReferencia = evento.DataEvento
                })
            });

            // Etapa 2: Verificar AutoCall
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Verificar AutoCall",
                OrdemExecucao = 2,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.VerificarAutoCall,
                DadosEntrada = "{}" // Será preenchido com dados da etapa anterior
            });

            // Etapa 3: Persistir AutoCall
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Persistir AutoCall",
                OrdemExecucao = 3,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.PersistirAutoCall,
                DadosEntrada = "{}" // Será preenchido com dados da etapa anterior
            });

            // Etapa 4: Notificar AutoCall
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Notificar AutoCall",
                OrdemExecucao = 4,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.NotificarAutoCall,
                DadosEntrada = "{}" // Será preenchido com dados da etapa anterior
            });

            await _orquestrador.IniciarSagaAsync(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no ObservadorAutoCall ao processar evento");
        }
    }
}

public class EventoBarreiraAtingida
{
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
}

