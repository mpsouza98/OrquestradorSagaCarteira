using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador de cotações que dispara verificação de barreiras em lote
/// </summary>
public class ObservadorBarreira : IObservador
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObservadorBarreira> _logger;

    public string NomeObservador => "ObservadorBarreira";

    public ObservadorBarreira(
        IServiceScopeFactory scopeFactory,
        ILogger<ObservadorBarreira> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("ObservadorBarreira recebeu evento do tópico {Topico}", topico);

            var evento = JsonSerializer.Deserialize<EventoCotacao>(dadosEvento);
            if (evento == null) return;

            using var scope = _scopeFactory.CreateScope();
            var barreiraRepository = scope.ServiceProvider.GetRequiredService<IBarreiraRepository>();
            var orquestrador = scope.ServiceProvider.GetRequiredService<IOrquestradorSaga>();

            // Buscar barreiras ativas para o ticker informado
            var barreirasDoTicker = await barreiraRepository.ObterBarreirasAtivasPorTickerAsync(evento.Ticker);

            _logger.LogInformation(
                "📊 Encontradas {Qtd} barreiras ativas para o ticker {Ticker}",
                barreirasDoTicker.Count, evento.Ticker);

            if (!barreirasDoTicker.Any())
            {
                _logger.LogInformation("➖ Nenhuma barreira ativa encontrada para processar");
                return;
            }

            // Iniciar Saga com 3 etapas em lote
            var saga = new Saga
            {
                Id = Guid.NewGuid(),
                TipoSaga = TipoSaga.ProcessamentoBarreira,
                EstadoSaga = EstadoSaga.Iniciada,
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow,
                DadosContexto = JsonSerializer.Serialize(new
                {
                    evento.Ticker,
                    CotacaoAtual = evento.PrecoFechamento,
                    DataEvento = evento.Data,
                    QuantidadeBarreiras = barreirasDoTicker.Count
                })
            };

            // Etapa 1: Verificar Barreiras (em lote)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Verificar Barreiras",
                OrdemExecucao = 1,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.VerificarBarreira,
                DadosEntrada = JsonSerializer.Serialize(new
                {
                    Ticker = evento.Ticker,
                    CotacaoAtual = evento.PrecoFechamento,
                    DataReferencia = evento.Data,
                    Barreiras = barreirasDoTicker.Select(b => new
                    {
                        BarreiraId = b.Id,
                        OperacaoId = b.OperacaoId,
                        NivelBarreira = b.NivelBarreira,
                        Condicao = b.Condicao
                    }).ToList()
                })
            });

            // Etapa 2: Persistir Barreiras Atingidas (em lote)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Persistir Barreiras",
                OrdemExecucao = 2,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.PersistirBarreira,
                DadosEntrada = "{}" // Será preenchido com resultado da etapa anterior
            });

            // Etapa 3: Notificar Barreiras Atingidas (em lote)
            saga.Etapas.Add(new EtapaSaga
            {
                Id = Guid.NewGuid(),
                SagaId = saga.Id,
                NomeEtapa = "Notificar Barreiras Atingidas",
                OrdemExecucao = 3,
                EstadoEtapa = EstadoEtapa.Pendente,
                TipoAcao = TipoAcao.NotificarBarreiraAtingida,
                DadosEntrada = "{}" // Será preenchido com resultado da etapa anterior
            });

            await orquestrador.IniciarSagaAsync(saga);

            _logger.LogInformation(
                "🚀 Saga {SagaId} iniciada para verificação de {Qtd} barreiras do ticker {Ticker}",
                saga.Id, barreirasDoTicker.Count, evento.Ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro no ObservadorBarreira ao processar evento");
        }
    }
}

public class EventoCotacao
{
    public string Ticker { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public decimal PrecoFechamento { get; set; }
    public string Fonte { get; set; } = string.Empty;
}
