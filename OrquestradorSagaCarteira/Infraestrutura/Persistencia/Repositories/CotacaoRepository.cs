using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class CotacaoRepository : ICotacaoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public CotacaoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Cotacao?> ObterCotacaoAsync(string ticker, DateTime data)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM cotacao 
            WHERE ticker_ativo = @Ticker 
              AND data = @Data 
            ORDER BY data_criacao DESC 
            LIMIT 1";
        
        return await connection.QuerySingleOrDefaultAsync<Cotacao>(
            sql, 
            new { Ticker = ticker, Data = data.Date });
    }

    public async Task<List<Cotacao>> ObterCotacoesPorTickersAsync(List<string> tickers, DateTime data)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM cotacao 
            WHERE ticker_ativo IN @Tickers 
              AND data = @Data";
        
        var cotacoes = await connection.QueryAsync<Cotacao>(
            sql, 
            new { Tickers = tickers, Data = data.Date });
        
        return cotacoes.ToList();
    }

    public async Task<Guid> InserirAsync(Cotacao cotacao)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            INSERT INTO cotacao (id, ticker_ativo, fonte, data, preco_fechamento, data_criacao)
            VALUES (@Id, @TickerAtivo, @Fonte, @Data, @PrecoFechamento, @DataCriacao)";

        await connection.ExecuteAsync(sql, cotacao);
        return cotacao.Id;
    }
}

