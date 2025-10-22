using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class LiquidacaoRepository : ILiquidacaoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public LiquidacaoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> InserirAsync(LiquidacaoAgendada liquidacao)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            INSERT INTO liquidacao_agendada (id, operacao_id, data_agendamento, data_liquidacao, 
                                             valor_liquidacao, status, motivo, dados_liquidacao)
            VALUES (@Id, @OperacaoId, @DataAgendamento, @DataLiquidacao, 
                    @ValorLiquidacao, @Status, @Motivo, @DadosLiquidacao)";

        await connection.ExecuteAsync(sql, liquidacao);
        return liquidacao.Id;
    }

    public async Task<List<LiquidacaoAgendada>> ObterAgendadasAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM liquidacao_agendada 
            WHERE status = 'Agendada' 
            ORDER BY data_liquidacao";
        
        var liquidacoes = await connection.QueryAsync<LiquidacaoAgendada>(sql);
        return liquidacoes.ToList();
    }

    public async Task AtualizarAsync(LiquidacaoAgendada liquidacao)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            UPDATE liquidacao_agendada 
            SET status = @Status, 
                data_processamento = @DataProcessamento,
                dados_liquidacao = @DadosLiquidacao
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, liquidacao);
    }
}

