using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface ICotacaoRepository
{
    Task<Cotacao?> ObterCotacaoAsync(string ticker, DateTime data);
    Task<List<Cotacao>> ObterCotacoesPorTickersAsync(List<string> tickers, DateTime data);
    Task<Guid> InserirAsync(Cotacao cotacao);
}

