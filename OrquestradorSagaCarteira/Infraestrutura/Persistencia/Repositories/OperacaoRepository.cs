using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class OperacaoRepository : IOperacaoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public OperacaoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Operacao?> ObterPorIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM operacao WHERE id = @Id;
            SELECT * FROM ativo_operacao WHERE operacao_id = @Id;
            SELECT * FROM barreira_operacao WHERE operacao_id = @Id;";

        using var multi = await connection.QueryMultipleAsync(sql, new { Id = id });
        var operacao = await multi.ReadSingleOrDefaultAsync<Operacao>();
        if (operacao != null)
        {
            operacao.Ativos = (await multi.ReadAsync<AtivoOperacao>()).ToList();
            operacao.Barreiras = (await multi.ReadAsync<BarreiraOperacao>()).ToList();
        }
        return operacao;
    }

    public async Task<Operacao?> ObterPorCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM operacao WHERE codigo_operacao = @Codigo;
            SELECT ao.* FROM ativo_operacao ao 
            INNER JOIN operacao o ON ao.operacao_id = o.id 
            WHERE o.codigo_operacao = @Codigo;
            SELECT bo.* FROM barreira_operacao bo 
            INNER JOIN operacao o ON bo.operacao_id = o.id 
            WHERE o.codigo_operacao = @Codigo;";

        using var multi = await connection.QueryMultipleAsync(sql, new { Codigo = codigo });
        var operacao = await multi.ReadSingleOrDefaultAsync<Operacao>();
        if (operacao != null)
        {
            operacao.Ativos = (await multi.ReadAsync<AtivoOperacao>()).ToList();
            operacao.Barreiras = (await multi.ReadAsync<BarreiraOperacao>()).ToList();
        }
        return operacao;
    }

    public async Task<List<Operacao>> ListarAtivasAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM operacao WHERE ativa = TRUE ORDER BY data_criacao DESC";
        var operacoes = await connection.QueryAsync<Operacao>(sql);
        return operacoes.ToList();
    }

    public async Task<Guid> InserirAsync(Operacao operacao)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO operacao (id, codigo_operacao, descricao, data_criacao, data_vencimento, 
                                  valor_nominal, tipo_estrutura, ativa, data_atualizacao)
            VALUES (@Id, @CodigoOperacao, @Descricao, @DataCriacao, @DataVencimento, 
                    @ValorNominal, @TipoEstrutura, @Ativa, @DataAtualizacao)";

        await connection.ExecuteAsync(sql, operacao);

        // Inserir ativos
        if (operacao.Ativos.Any())
        {
            const string sqlAtivo = @"
                INSERT INTO ativo_operacao (id, operacao_id, ticker, cotacao_inicial, 
                                            percentual_participacao, data_criacao)
                VALUES (@Id, @OperacaoId, @Ticker, @CotacaoInicial, 
                        @PercentualParticipacao, @DataCriacao)";
            await connection.ExecuteAsync(sqlAtivo, operacao.Ativos);
        }

        // Inserir barreiras
        if (operacao.Barreiras.Any())
        {
            const string sqlBarreira = @"
                INSERT INTO barreira_operacao (id, operacao_id, ticker, tipo_barreira, condicao, 
                                               nivel_barreira, data_observacao, atingida, ativa, data_criacao)
                VALUES (@Id, @OperacaoId, @Ticker, @TipoBarreira, @Condicao, 
                        @NivelBarreira, @DataObservacao, @Atingida, @Ativa, @DataCriacao)";
            await connection.ExecuteAsync(sqlBarreira, operacao.Barreiras);
        }

        return operacao.Id;
    }

    public async Task AtualizarAsync(Operacao operacao)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE operacao 
            SET descricao = @Descricao, 
                data_vencimento = @DataVencimento, 
                valor_nominal = @ValorNominal, 
                tipo_estrutura = @TipoEstrutura, 
                ativa = @Ativa, 
                data_atualizacao = @DataAtualizacao
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, operacao);
    }
}
