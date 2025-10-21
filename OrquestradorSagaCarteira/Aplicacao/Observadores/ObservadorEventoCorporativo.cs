using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos corporativos (split, insplit, etc)
/// </summary>
public class ObservadorEventoCorporativo : IObservador
{
    private readonly ILogger<ObservadorEventoCorporativo> _logger;
    private readonly IOrquestradorSaga _orquestradorSaga;

    public string NomeObservador => "ObservadorEventoCorporativo";

    public ObservadorEventoCorporativo(ILogger<ObservadorEventoCorporativo> logger, IOrquestradorSaga orquestradorSaga)
    {
        _logger = logger;
        _orquestradorSaga = orquestradorSaga;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de Evento Corporativo recebeu evento do tópico {Topico}", topico);
            
            var evento = JsonSerializer.Deserialize<EventoCorporativoData>(dadosEvento);

            TipoAcao tipoAcao = evento!.TipoEvento switch
            {
                "Split" => TipoAcao.ProcessarSplit,
                "Inplit" => TipoAcao.ProcessarInsplit,
                _ => TipoAcao.AjustarPosicoes
            };

            // Criar saga de processamento de evento corporativo
            var saga = new Saga
            {
                TipoSaga = TipoSaga.ProcessamentoEventoCorporativo,
                DadosContexto = dadosEvento,
                Etapas = new List<EtapaSaga>
                {
                    new EtapaSaga
                    {
                        NomeEtapa = $"Processar {evento.TipoEvento}",
                        TipoAcao = tipoAcao,
                        DadosEntrada = JsonSerializer.Serialize(new { EventoId = evento.EventoId })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Ajustar Posições",
                        TipoAcao = TipoAcao.AjustarPosicoes,
                        DadosEntrada = JsonSerializer.Serialize(new { EventoId = evento.EventoId })
                    },
                    new EtapaSaga
                    {
                        NomeEtapa = "Realizar Ajuste Contábil",
                        TipoAcao = TipoAcao.RealizarAjusteContabil,
                        DadosEntrada = JsonSerializer.Serialize(new { EventoId = evento.EventoId })
                    }
                }
            };

            await _orquestradorSaga.IniciarSagaAsync(saga);
            
            _logger.LogInformation("Saga de evento corporativo {TipoEvento} iniciada", evento.TipoEvento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento corporativo");
        }
    }

    private class EventoCorporativoData
    {
        public Guid EventoId { get; set; }
        public string TipoEvento { get; set; } = string.Empty;
        public string CodigoAtivo { get; set; } = string.Empty;
        public decimal? FatorAjuste { get; set; }
    }
}

