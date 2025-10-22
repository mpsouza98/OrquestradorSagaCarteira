using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class BarreiraRepository : IBarreiraRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    private const string CamposSelecionados = @"
            id AS Id,
            operacao_id AS OperacaoId,
            ticker AS Ticker,
            tipo_barreira AS TipoBarreira,
            condicao AS Condicao,
            nivel_barreira AS NivelBarreira,
            data_observacao AS DataObservacao,
            atingida AS Atingida,
            data_atingimento AS DataAtingimento,
            valor_atingimento AS ValorAtingimento,
            ativa AS Ativa,
            data_criacao AS DataCriacao";

    public BarreiraRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BarreiraOperacao?> ObterPorIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {CamposSelecionados} FROM barreira_operacao WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<BarreiraOperacao>(sql, new { Id = id });
    }

    public async Task<List<BarreiraOperacao>> ObterPorOperacaoAsync(Guid operacaoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {CamposSelecionados} FROM barreira_operacao WHERE operacao_id = @OperacaoId ORDER BY data_observacao";
        var barreiras = await connection.QueryAsync<BarreiraOperacao>(sql, new { OperacaoId = operacaoId });
        return barreiras.ToList();
    }
    public async Task<List<BarreiraOperacao>> ObterBarreirasAtivasPorTickerAsync(string ticker)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = $@"
            SELECT {CamposSelecionados} FROM barreira_operacao 
            WHERE ativa = TRUE 
              AND atingida = FALSE 
              AND ticker = @Ticker
            ORDER BY data_observacao";
        var barreiras = await connection.QueryAsync<BarreiraOperacao>(sql, new { Ticker = ticker });
        return barreiras.ToList();
    }

    public async Task<Guid> InserirAsync(BarreiraOperacao barreira)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO barreira_operacao (id, operacao_id, ticker, tipo_barreira, condicao, 
                                           nivel_barreira, data_observacao, atingida, ativa, data_criacao)
            VALUES (@Id, @OperacaoId, @Ticker, @TipoBarreira, @Condicao, 
                    @NivelBarreira, @DataObservacao, @Atingida, @Ativa, @DataCriacao)";
        await connection.ExecuteAsync(sql, barreira);
        return barreira.Id;
    }

    public async Task AtualizarAsync(BarreiraOperacao barreira)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE barreira_operacao 
            SET atingida = @Atingida, 
                data_atingimento = @DataAtingimento, 
                valor_atingimento = @ValorAtingimento,
                ativa = @Ativa
            WHERE id = @Id";
        await connection.ExecuteAsync(sql, barreira);
    }
}
