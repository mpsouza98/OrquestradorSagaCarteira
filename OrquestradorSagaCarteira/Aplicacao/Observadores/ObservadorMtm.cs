using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos de MTM e inicia saga de valorização
/// </summary>
public class ObservadorMtm : IObservador
{
    private readonly ILogger<ObservadorMtm> _logger;
    private readonly IOrquestradorSaga _orquestradorSaga;

    public string NomeObservador => "ObservadorMtm";

    public ObservadorMtm(ILogger<ObservadorMtm> logger, IOrquestradorSaga orquestradorSaga)
    {
        _logger = logger;
        _orquestradorSaga = orquestradorSaga;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de MTM recebeu evento do tópico {Topico}", topico);
            
            var dadosMtm = JsonSerializer.Deserialize<EventoMtm>(dadosEvento);

            // Criar saga de valorização MTM
            var saga = new Saga
            {
                TipoSaga = TipoSaga.ValorizacaoMtm,
                DadosContexto = dadosEvento,
                Etapas = new List<EtapaSaga>
                {
                    new EtapaSaga
                    {
                        NomeEtapa = "Calcular MTM Renda Fixa",
                        TipoAcao = TipoAcao.CalcularMtmRendaFixa,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosMtm!.CoeId, 
                            DataReferencia = dadosMtm.DataReferencia 
                        })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Calcular MTM Renda Variável",
                        TipoAcao = TipoAcao.CalcularMtmRendaVariavel,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosMtm.CoeId, 
                            DataReferencia = dadosMtm.DataReferencia 
                        })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Consolidar MTM",
                        TipoAcao = TipoAcao.ConsolidarMtm,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosMtm.CoeId, 
                            DataReferencia = dadosMtm.DataReferencia,
                            ValorRendaFixa = 0m,
                            ValorRendaVariavel = 0m
                        })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Calcular Valorização Contábil",
                        TipoAcao = TipoAcao.CalcularValorizacaoContabil,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosMtm.CoeId, 
                            DataReferencia = dadosMtm.DataReferencia 
                        })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Atualizar Posição Cliente",
                        TipoAcao = TipoAcao.AtualizarPosicaoCliente,
                        DadosEntrada = JsonSerializer.Serialize(new 
                        { 
                            CoeId = dadosMtm.CoeId, 
                            DataReferencia = dadosMtm.DataReferencia 
                        })
                    }
                }
            };

            await _orquestradorSaga.IniciarSagaAsync(saga);
            
            _logger.LogInformation("Saga de valorização MTM iniciada para COE {CoeId}", dadosMtm.CoeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento de MTM");
        }
    }

    private class EventoMtm
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

