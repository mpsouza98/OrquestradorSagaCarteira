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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObservadorAutoCall> _logger;

    public string NomeObservador => "ObservadorAutoCall";

    public ObservadorAutoCall(
        IServiceScopeFactory scopeFactory,
        ILogger<ObservadorAutoCall> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("ObservadorAutoCall recebeu evento do tópico {Topico}", topico);

            // Espera payload no formato: { OperacoesIds: [..], TipoEvento: "BarreirasAtingidas", DataEvento: ... }
            var batch = JsonSerializer.Deserialize<EventoBarreirasAtingidasBatch>(dadosEvento);
            if (batch == null || batch.OperacoesIds == null || !batch.OperacoesIds.Any())
            {
                _logger.LogWarning("Payload inválido ou sem operações para verificar autocall");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var orquestrador = scope.ServiceProvider.GetRequiredService<IOrquestradorSaga>();
            var barreiraRepository = scope.ServiceProvider.GetRequiredService<IBarreiraRepository>();

            // Carregar barreiras de todas as operações em uma única consulta
            var barreiras = await barreiraRepository.ObterPorOperacoesAsync(batch.OperacoesIds);

            // Montar DadosContexto compatível: lista de todas barreiras das operações
            var dadosContexto = JsonSerializer.Serialize(new
            {
                Ticker = string.Empty,
                CotacaoAtual = 0m,
                DataReferencia = batch.DataEvento,
                Fonte = "topico.barreira",
                Barreiras = barreiras.Select(b => new
                {
                    BarreiraId = b.Id,
                    OperacaoId = b.OperacaoId,
                    NivelBarreira = b.NivelBarreira,
                    Condicao = b.Condicao
                }).ToList()
            });

            // Criar saga para processar autocall em lote: Verificar -> Persistir -> Notificar
            var saga = new Saga
            {
                Id = Guid.NewGuid(),
                TipoSaga = TipoSaga.ProcessamentoAutocall,
                EstadoSaga = EstadoSaga.Iniciada,
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow,
                DadosContexto = dadosContexto
            };

            // Etapa 1: Verificar AutoCall (lê do contexto)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Verificar AutoCall",
                OrdemExecucao = 1,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.VerificarAutoCall,
                DadosEntrada = "{}"
            });

            // Etapa 2: Persistir AutoCall (lote)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Persistir AutoCall",
                OrdemExecucao = 2,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.PersistirAutoCall,
                DadosEntrada = "{}"
            });

            // Etapa 3: Notificar AutoCall (lote)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Notificar AutoCall",
                OrdemExecucao = 3,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.NotificarAutoCall,
                DadosEntrada = "{}"
            });

            await orquestrador.IniciarSagaAsync(saga);

            _logger.LogInformation(
                "🚀 Saga {SagaId} iniciada para verificação de autocall em {Qtd} operações",
                saga.Id, batch.OperacoesIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no ObservadorAutoCall ao processar evento");
        }
    }
}

public class EventoBarreirasAtingidasBatch
{
    public List<Guid> OperacoesIds { get; set; } = new();
    public string TipoEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
}
