using System.Text.Json;
using Calculadora.Core.Services;
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
            var operacaoRepository = scope.ServiceProvider.GetRequiredService<IOperacaoRepository>();
            var eventoBarreiraRepository = scope.ServiceProvider.GetRequiredService<IEventoBarreiraRepository>();
            var orquestrador = scope.ServiceProvider.GetRequiredService<IOrquestradorSaga>();

            // Buscar barreiras ativas para o ticker informado na data do evento
            var barreirasDoTicker = await barreiraRepository.ObterBarreirasAtivasPorTickerAsync(evento.Ticker);

            _logger.LogInformation(
                "📊 Encontradas {Qtd} barreiras para o ticker {Ticker}",
                barreirasDoTicker.Count, evento.Ticker);

            // Para cada barreira encontrada, verificar se foi atingida
            foreach (var barreira in barreirasDoTicker)
            {
                try
                {
                    _logger.LogInformation(
                        "🔍 Verificando barreira {BarreiraId} - Ticker: {Ticker}, Nivel: {Nivel}%, Condicao: {Condicao}",
                        barreira.Id, barreira.Ticker, barreira.NivelBarreira, barreira.Condicao);

                    // Obter operação para pegar cotação inicial do ativo
                    var operacao = await operacaoRepository.ObterPorIdAsync(barreira.OperacaoId);
                    if (operacao == null)
                    {
                        _logger.LogWarning("⚠️ Operação {OperacaoId} não encontrada para barreira {BarreiraId}",
                            barreira.OperacaoId, barreira.Id);
                        continue;
                    }

                    var ativo = operacao.Ativos.FirstOrDefault(a => 
                        string.Equals(a.Ticker, evento.Ticker, StringComparison.OrdinalIgnoreCase));
                    
                    if (ativo == null)
                    {
                        _logger.LogWarning("⚠️ Ativo {Ticker} não encontrado na operação {OperacaoId}",
                            evento.Ticker, operacao.Id);
                        continue;
                    }

                    // Verificar se a barreira foi atingida usando a calculadora
                    var resultado = CalculadoraBarreira.VerificarBarreira(
                        ativo.CotacaoInicial,
                        evento.PrecoFechamento,
                        barreira.NivelBarreira,
                        barreira.Condicao);

                    _logger.LogInformation(
                        "📈 Resultado verificação - Barreira: {BarreiraId}, Atingida: {Atingida}, Taxa: {Taxa}%",
                        barreira.Id, resultado.BarreiraAtingida, resultado.TaxaVariacao);

                    // Se a barreira foi atingida, atualizar o estado e criar evento
                    if (resultado.BarreiraAtingida)
                    {
                        // Atualizar estado da barreira
                        barreira.Atingida = true;
                        barreira.DataAtingimento = DateTime.UtcNow;
                        barreira.ValorAtingimento = evento.PrecoFechamento;
                        await barreiraRepository.AtualizarAsync(barreira);

                        _logger.LogInformation(
                            "✅ Barreira {BarreiraId} ATINGIDA e persistida - Ticker: {Ticker}, Valor: {Valor}",
                            barreira.Id, evento.Ticker, evento.PrecoFechamento);

                        // Criar evento de barreira atingida
                        var eventoBarreira = new EventoBarreira
                        {
                            Id = Guid.NewGuid(),
                            BarreiraId = barreira.Id,
                            OperacaoId = barreira.OperacaoId,
                            Ticker = barreira.Ticker,
                            ValorObservado = evento.PrecoFechamento,
                            NivelBarreira = barreira.NivelBarreira,
                            TipoBarreira = barreira.TipoBarreira,
                            DataEvento = DateTime.UtcNow,
                            DadosEvento = JsonSerializer.Serialize(new
                            {
                                CotacaoInicial = ativo.CotacaoInicial,
                                CotacaoAtual = evento.PrecoFechamento,
                                resultado.TaxaVariacao,
                                barreira.Condicao,
                                Fonte = evento.Fonte
                            })
                        };

                        await eventoBarreiraRepository.InserirAsync(eventoBarreira);

                        _logger.LogInformation(
                            "💾 Evento de barreira criado - EventoId: {EventoId}",
                            eventoBarreira.Id);

                        // Iniciar saga para processar a barreira atingida
                        var saga = new Saga
                        {
                            Id = Guid.NewGuid(),
                            TipoSaga = TipoSaga.ProcessamentoAutocall,
                            EstadoSaga = EstadoSaga.Iniciada,
                            DataCriacao = DateTime.UtcNow,
                            DataAtualizacao = DateTime.UtcNow,
                            DadosContexto = JsonSerializer.Serialize(new
                            {
                                evento.Ticker,
                                BarreiraId = barreira.Id,
                                EventoBarreiraId = eventoBarreira.Id,
                                TaxaVariacao = resultado.TaxaVariacao
                            })
                        };

                        // Etapa 1: Persistir Barreira (já feito, mas mantém na saga para auditoria)
                        saga.Etapas.Add(new EtapaSaga
                        {
                            Id = Guid.NewGuid(),
                            SagaId = saga.Id,
                            NomeEtapa = "Persistir Barreira",
                            OrdemExecucao = 1,
                            EstadoEtapa = EstadoEtapa.Pendente,
                            TipoAcao = TipoAcao.PersistirBarreira,
                            DadosEntrada = JsonSerializer.Serialize(new
                            {
                                BarreiraId = barreira.Id,
                                evento.Ticker,
                                BarreiraAtingida = true,
                                ValorObservado = evento.PrecoFechamento,
                                TaxaVariacao = resultado.TaxaVariacao
                            })
                        });

                        // Etapa 2: Notificar Barreira Atingida
                        saga.Etapas.Add(new EtapaSaga
                        {
                            Id = Guid.NewGuid(),
                            SagaId = saga.Id,
                            NomeEtapa = "Notificar Barreira Atingida",
                            OrdemExecucao = 2,
                            EstadoEtapa = EstadoEtapa.Pendente,
                            TipoAcao = TipoAcao.NotificarBarreiraAtingida,
                            DadosEntrada = JsonSerializer.Serialize(new
                            {
                                EventoId = eventoBarreira.Id,
                                OperacaoId = barreira.OperacaoId
                            })
                        });

                        await orquestrador.IniciarSagaAsync(saga);

                        _logger.LogInformation(
                            "🚀 Saga {SagaId} iniciada para barreira atingida {BarreiraId}",
                            saga.Id, barreira.Id);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "➖ Barreira {BarreiraId} NÃO atingida - Ticker: {Ticker}, Taxa atual: {Taxa}%, Nivel requerido: {Nivel}%",
                            barreira.Id, evento.Ticker, resultado.TaxaVariacao, barreira.NivelBarreira);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "❌ Erro ao processar barreira {BarreiraId} do ticker {Ticker}",
                        barreira.Id, evento.Ticker);
                }
            }
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
