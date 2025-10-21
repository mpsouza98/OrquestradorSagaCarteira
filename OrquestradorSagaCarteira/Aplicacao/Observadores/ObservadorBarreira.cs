using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos de barreiras e inicia saga de autocall
/// Implementa lógica de worst-off: verifica se todos os ativos da cesta atingiram barreira
/// </summary>
public class ObservadorBarreira : IObservador
{
    private readonly ILogger<ObservadorBarreira> _logger;
    private readonly IOrquestradorSaga _orquestradorSaga;
    private readonly CarteiraDbContext _context;

    public string NomeObservador => "ObservadorBarreira";

    public ObservadorBarreira(
        ILogger<ObservadorBarreira> logger, 
        IOrquestradorSaga orquestradorSaga,
        CarteiraDbContext context)
    {
        _logger = logger;
        _orquestradorSaga = orquestradorSaga;
        _context = context;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de Barreira recebeu evento do tópico {Topico}", topico);
            
            var dadosBarreira = JsonSerializer.Deserialize<EventoBarreira>(dadosEvento);

            if (dadosBarreira == null)
            {
                _logger.LogWarning("Dados de barreira inválidos");
                return;
            }

            // Marcar barreira como atingida
            var barreira = await _context.Barreiras
                .Include(b => b.Coe)
                .ThenInclude(c => c.Ativos)
                .FirstOrDefaultAsync(b => b.Id == dadosBarreira.BarreiraId);

            if (barreira == null)
            {
                _logger.LogWarning("Barreira {BarreiraId} não encontrada", dadosBarreira.BarreiraId);
                return;
            }

            // Atualizar barreira
            barreira.Atingida = true;
            barreira.DataAtingimento = DateTime.UtcNow;
            barreira.ValorAtingimento = dadosBarreira.ValorAtingimento;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Barreira {BarreiraId} marcada como atingida", barreira.Id);

            // Se for tipo Autocall, verificar se todas as barreiras do COE foram atingidas (worst-off)
            if (barreira.TipoBarreira == TipoBarreira.Autocall || barreira.TipoBarreira == TipoBarreira.WorstOf)
            {
                await ProcessarAutocallWorstOff(barreira);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento de barreira");
        }
    }

    private async Task ProcessarAutocallWorstOff(Barreira barreiraAtingida)
    {
        try
        {
            // Buscar todas as barreiras do COE para a mesma data de observação
            var todasBarreiras = await _context.Barreiras
                .Where(b => b.CoeId == barreiraAtingida.CoeId 
                    && b.DataObservacao == barreiraAtingida.DataObservacao
                    && b.Ativa)
                .ToListAsync();

            var totalBarreiras = todasBarreiras.Count;
            var barreirasAtingidas = todasBarreiras.Count(b => b.Atingida);

            _logger.LogInformation(
                "COE {CoeId}: {Atingidas}/{Total} barreiras atingidas para data {Data}",
                barreiraAtingida.CoeId, barreirasAtingidas, totalBarreiras, barreiraAtingida.DataObservacao);

            // Worst-off: Todos os ativos devem atingir a barreira
            if (barreirasAtingidas == totalBarreiras && totalBarreiras > 0)
            {
                _logger.LogInformation(
                    "🎯 AUTOCALL ATIVADO! Todas as barreiras do COE {CoeId} foram atingidas (Worst-Off)",
                    barreiraAtingida.CoeId);

                // Buscar cotações para calcular variação
                var coe = await _context.Coes
                    .Include(c => c.Ativos)
                    .FirstOrDefaultAsync(c => c.Id == barreiraAtingida.CoeId);

                if (coe != null)
                {
                    var cotacoes = await _context.Cotacoes
                        .Where(c => c.Data == barreiraAtingida.DataObservacao)
                        .ToListAsync();

                    // Calcular variações e identificar worst-off (menor variação)
                    decimal menorVariacao = decimal.MaxValue;
                    string? ativoWorstOff = null;

                    foreach (var ativo in coe.Ativos)
                    {
                        var cotacao = cotacoes.FirstOrDefault(c => c.TickerAtivo == ativo.TickerAtivo);
                        if (cotacao != null && ativo.CotacaoInicial > 0)
                        {
                            var variacao = ((cotacao.PrecoFechamento - ativo.CotacaoInicial) / ativo.CotacaoInicial) * 100;
                            
                            _logger.LogInformation(
                                "Ativo {Ticker}: Cotação Inicial={Inicial}, Cotação Dia={Dia}, Variação={Variacao}%",
                                ativo.TickerAtivo, ativo.CotacaoInicial, cotacao.PrecoFechamento, variacao);

                            if (variacao < menorVariacao)
                            {
                                menorVariacao = variacao;
                                ativoWorstOff = ativo.TickerAtivo;
                            }
                        }
                    }

                    _logger.LogInformation(
                        "Ativo Worst-Off: {Ticker} com variação de {Variacao}%",
                        ativoWorstOff, menorVariacao);

                    // Iniciar saga de autocall/liquidação
                    await IniciarSagaAutocall(barreiraAtingida, ativoWorstOff, menorVariacao);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar autocall worst-off");
        }
    }

    private async Task IniciarSagaAutocall(Barreira barreira, string? ativoWorstOff, decimal variacao)
    {
        var saga = new Saga
        {
            TipoSaga = TipoSaga.AtivacaoAutocall,
            DadosContexto = JsonSerializer.Serialize(new 
            { 
                CoeId = barreira.CoeId,
                BarreiraId = barreira.Id,
                DataObservacao = barreira.DataObservacao,
                AtivoWorstOff = ativoWorstOff,
                VariacaoWorstOff = variacao
            }),
            Etapas = new List<EtapaSaga>
            {
                new EtapaSaga
                {
                    NomeEtapa = "Verificar Barreira",
                    TipoAcao = TipoAcao.VerificarBarreira,
                    DadosEntrada = JsonSerializer.Serialize(new { BarreiraId = barreira.Id })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Processar Atingimento",
                    TipoAcao = TipoAcao.ProcessarAtingimentoBarreira,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        BarreiraId = barreira.Id,
                        BarreiraAtingida = true,
                        ValorCesta = barreira.ValorAtingimento ?? 0m
                    })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Iniciar Liquidação",
                    TipoAcao = TipoAcao.IniciarLiquidacao,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        CoeId = barreira.CoeId,
                        BarreiraId = barreira.Id,
                        DataLiquidacao = barreira.DataObservacao.AddDays(2) // D+2
                    })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Calcular Valor Liquidação",
                    TipoAcao = TipoAcao.CalcularValorLiquidacao,
                    DadosEntrada = JsonSerializer.Serialize(new { LiquidacaoId = Guid.Empty })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Liquidar Posições",
                    TipoAcao = TipoAcao.LiquidarPosicoes,
                    DadosEntrada = JsonSerializer.Serialize(new { LiquidacaoId = Guid.Empty })
                },
                new EtapaSaga
                {
                    NomeEtapa = "Encerrar COE",
                    TipoAcao = TipoAcao.EncerrarCoe,
                    DadosEntrada = JsonSerializer.Serialize(new 
                    { 
                        CoeId = barreira.CoeId,
                        LiquidacaoId = Guid.Empty
                    })
                }
            }
        };

        await _orquestradorSaga.IniciarSagaAsync(saga);
        _logger.LogInformation("Saga de Autocall iniciada para COE {CoeId}", barreira.CoeId);
    }

    private class EventoBarreira
    {
        public Guid BarreiraId { get; set; }
        public Guid CoeId { get; set; }
        public string TipoBarreira { get; set; } = string.Empty;
        public string? TickerAtivo { get; set; }
        public decimal ValorAtingimento { get; set; }
        public DateTime DataObservacao { get; set; }
    }
}
