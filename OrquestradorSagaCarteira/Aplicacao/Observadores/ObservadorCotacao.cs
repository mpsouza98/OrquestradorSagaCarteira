using OrquestradorSagaCarteira.Dominio.Interfaces;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos de cotações
/// </summary>
public class ObservadorCotacao : IObservador
{
    private readonly ILogger<ObservadorCotacao> _logger;
    private readonly IOrquestradorSaga _orquestradorSaga;

    public string NomeObservador => "ObservadorCotacao";

    public ObservadorCotacao(ILogger<ObservadorCotacao> logger, IOrquestradorSaga orquestradorSaga)
    {
        _logger = logger;
        _orquestradorSaga = orquestradorSaga;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de Cotação recebeu evento do tópico {Topico}", topico);
            
            var dadosCotacao = JsonSerializer.Deserialize<EventoCotacao>(dadosEvento);
            
            // Aqui você pode processar a cotação ou iniciar uma saga
            _logger.LogInformation("Cotação processada: {CodigoAtivo} = {Preco}", 
                dadosCotacao?.CodigoAtivo, dadosCotacao?.PrecoFechamento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento de cotação");
        }
    }

    private class EventoCotacao
    {
        public string CodigoAtivo { get; set; } = string.Empty;
        public decimal? PrecoFechamento { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

