using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class BarreiraRepository : IBarreiraRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public BarreiraRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BarreiraOperacao?> ObterPorIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM barreira_operacao WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<BarreiraOperacao>(sql, new { Id = id });
    }

    public async Task<List<BarreiraOperacao>> ObterPorOperacaoAsync(Guid operacaoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM barreira_operacao WHERE operacao_id = @OperacaoId ORDER BY data_observacao";
        var barreiras = await connection.QueryAsync<BarreiraOperacao>(sql, new { OperacaoId = operacaoId });
        return barreiras.ToList();
    }

    public async Task<List<BarreiraOperacao>> ObterBarreirasAtivasParaDataAsync(DateTime data)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM barreira_operacao 
            WHERE ativa = TRUE 
              AND atingida = FALSE 
              AND data_observacao <= @Data
            ORDER BY data_observacao";
        var barreiras = await connection.QueryAsync<BarreiraOperacao>(sql, new { Data = data });
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
