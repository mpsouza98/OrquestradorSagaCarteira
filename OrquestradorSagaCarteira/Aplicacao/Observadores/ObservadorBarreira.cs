using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador de cotações que dispara verificação de barreiras
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

            // Buscar barreiras ativas para verificação
            var barreiras = await barreiraRepository.ObterBarreirasAtivasParaDataAsync(evento.Data);
            
            foreach (var barreira in barreiras.Where(b => b.Ticker == evento.Ticker))
            {
                _logger.LogInformation(
                    "Iniciando saga para verificação de barreira - Ticker: {Ticker}, Barreira: {BarreiraId}",
                    evento.Ticker, barreira.Id);

                // Criar saga para processar barreira
                var saga = new Saga
                {
                    Id = Guid.NewGuid(),
                    TipoSaga = TipoSaga.ProcessamentoAutocall,
                    EstadoSaga = EstadoSaga.Iniciada,
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow,
                    DadosContexto = JsonSerializer.Serialize(new { evento.Ticker, BarreiraId = barreira.Id })
                };

                // Etapa 1: Verificar Barreira
                saga.Etapas.Add(new EtapaSaga
                {
                    Id = Guid.NewGuid(),
                    SagaId = saga.Id,
                    NomeEtapa = "Verificar Barreira",
                    OrdemExecucao = 1,
                    EstadoEtapa = EstadoEtapa.Pendente,
                    TipoAcao = TipoAcao.VerificarBarreira,
                    DadosEntrada = JsonSerializer.Serialize(new
                    {
                        BarreiraId = barreira.Id,
                        evento.Ticker,
                        CotacaoAtual = evento.PrecoFechamento
                    })
                });

                // Etapa 2: Persistir Barreira
                saga.Etapas.Add(new EtapaSaga
                {
                    Id = Guid.NewGuid(),
                    SagaId = saga.Id,
                    NomeEtapa = "Persistir Barreira",
                    OrdemExecucao = 2,
                    EstadoEtapa = EstadoEtapa.Pendente,
                    TipoAcao = TipoAcao.PersistirBarreira,
                    DadosEntrada = "{}" // Será preenchido com dados da etapa anterior
                });

                // Etapa 3: Notificar Barreira Atingida
                saga.Etapas.Add(new EtapaSaga
                {
                    Id = Guid.NewGuid(),
                    SagaId = saga.Id,
                    NomeEtapa = "Notificar Barreira Atingida",
                    OrdemExecucao = 3,
                    EstadoEtapa = EstadoEtapa.Pendente,
                    TipoAcao = TipoAcao.NotificarBarreiraAtingida,
                    DadosEntrada = "{}" // Será preenchido com dados da etapa anterior
                });

                await orquestrador.IniciarSagaAsync(saga);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no ObservadorBarreira ao processar evento");
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
