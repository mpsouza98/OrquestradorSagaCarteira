using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos de barreiras e inicia saga de autocall
/// </summary>
public class ObservadorBarreira : IObservador
{
    private readonly ILogger<ObservadorBarreira> _logger;
    private readonly IOrquestradorSaga _orquestradorSaga;

    public string NomeObservador => "ObservadorBarreira";

    public ObservadorBarreira(ILogger<ObservadorBarreira> logger, IOrquestradorSaga orquestradorSaga)
    {
        _logger = logger;
        _orquestradorSaga = orquestradorSaga;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de Barreira recebeu evento do tópico {Topico}", topico);
            
            var dadosBarreira = JsonSerializer.Deserialize<EventoBarreira>(dadosEvento);

            // Criar saga de processamento de barreira
            var saga = new Saga
            {
                TipoSaga = TipoSaga.ProcessamentoBarreira,
                DadosContexto = dadosEvento,
                Etapas = new List<EtapaSaga>
                {
                    new EtapaSaga
                    {
                        NomeEtapa = "Verificar Barreira",
                        TipoAcao = TipoAcao.VerificarBarreira,
                        DadosEntrada = JsonSerializer.Serialize(new { BarreiraId = dadosBarreira!.BarreiraId })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Processar Atingimento",
                        TipoAcao = TipoAcao.ProcessarAtingimentoBarreira,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            BarreiraId = dadosBarreira.BarreiraId,
                            BarreiraAtingida = false,
                            ValorCesta = 0m
                        })
                    }
                }
            };

            // Se for autocall e barreira foi atingida, adicionar etapas de liquidação
            if (dadosBarreira.TipoBarreira == "Autocall")
            {
                saga.TipoSaga = TipoSaga.AtivacaoAutocall;
                saga.Etapas.AddRange(new[]
                {
                    new EtapaSaga
                    {
                        NomeEtapa = "Iniciar Liquidação",
                        TipoAcao = TipoAcao.IniciarLiquidacao,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosBarreira.CoeId,
                            BarreiraId = dadosBarreira.BarreiraId,
                            DataLiquidacao = DateTime.Today
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
                            CoeId = dadosBarreira.CoeId,
                            LiquidacaoId = Guid.Empty
                        })
                    }
                });
            }

            await _orquestradorSaga.IniciarSagaAsync(saga);
            
            _logger.LogInformation("Saga de barreira iniciada para barreira {BarreiraId}", dadosBarreira.BarreiraId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento de barreira");
        }
    }

    private class EventoBarreira
    {
        public Guid BarreiraId { get; set; }
        public Guid CoeId { get; set; }
        public string TipoBarreira { get; set; } = string.Empty;
        public DateTime DataObservacao { get; set; }
    }
}

