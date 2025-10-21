using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Observadores;

/// <summary>
/// Observador que processa eventos de cotações e verifica barreiras
/// </summary>
public class ObservadorCotacao : IObservador
{
    private readonly ILogger<ObservadorCotacao> _logger;
    private readonly CarteiraDbContext _context;
    private readonly IPublicadorEventos _publicador;

    public string NomeObservador => "ObservadorCotacao";

    public ObservadorCotacao(
        ILogger<ObservadorCotacao> logger, 
        CarteiraDbContext context,
        IPublicadorEventos publicador)
    {
        _logger = logger;
        _context = context;
        _publicador = publicador;
    }

    public async Task NotificarAsync(string topico, string dadosEvento)
    {
        try
        {
            _logger.LogInformation("Observador de Cotação recebeu evento do tópico {Topico}", topico);
            
            var dadosCotacao = JsonSerializer.Deserialize<EventoCotacao>(dadosEvento);
            
            if (dadosCotacao == null)
            {
                _logger.LogWarning("Dados de cotação inválidos");
                return;
            }

            // Buscar barreiras ativas para o ticker da cotação
            var barreiras = await _context.Barreiras
                .Include(b => b.Coe)
                .ThenInclude(c => c.Ativos)
                .Where(b => b.TickerAtivo == dadosCotacao.TickerAtivo 
                    && b.Ativa 
                    && !b.Atingida
                    && b.DataObservacao <= dadosCotacao.Data)
                .ToListAsync();

            _logger.LogInformation("Verificando {Count} barreiras para {Ticker}", 
                barreiras.Count, dadosCotacao.TickerAtivo);

            foreach (var barreira in barreiras)
            {
                // Verificar se a barreira foi atingida
                bool atingida = false;
                
                if (barreira.Condicao == "UP")
                {
                    atingida = dadosCotacao.PrecoFechamento >= barreira.NivelBarreira;
                }
                else if (barreira.Condicao == "DOWN")
                {
                    atingida = dadosCotacao.PrecoFechamento <= barreira.NivelBarreira;
                }

                if (atingida)
                {
                    _logger.LogInformation(
                        "Barreira {BarreiraId} atingida! Ticker: {Ticker}, Preço: {Preco}, Nível: {Nivel}, Condição: {Condicao}",
                        barreira.Id, dadosCotacao.TickerAtivo, dadosCotacao.PrecoFechamento, 
                        barreira.NivelBarreira, barreira.Condicao);

                    // Publicar evento de barreira atingida
                    await _publicador.PublicarAsync("topico.barreiras", "BarreiraAtingida", new
                    {
                        BarreiraId = barreira.Id,
                        CoeId = barreira.CoeId,
                        TipoBarreira = barreira.TipoBarreira.ToString(),
                        TickerAtivo = dadosCotacao.TickerAtivo,
                        ValorAtingimento = dadosCotacao.PrecoFechamento,
                        DataObservacao = dadosCotacao.Data
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento de cotação");
        }
    }

    private class EventoCotacao
    {
        public string TickerAtivo { get; set; } = string.Empty;
        public decimal PrecoFechamento { get; set; }
        public DateTime Data { get; set; }
    }
}
