using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class SagaRepository : ISagaRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public SagaRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Saga?> ObterPorIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM saga WHERE id = @Id;
            SELECT * FROM etapa_saga WHERE saga_id = @Id ORDER BY ordem_execucao;";

        using var multi = await connection.QueryMultipleAsync(sql, new { Id = id });
        
        var saga = await multi.ReadSingleOrDefaultAsync<Saga>();
        if (saga != null)
        {
            saga.Etapas = (await multi.ReadAsync<EtapaSaga>()).ToList();
        }

        return saga;
    }

    public async Task<List<Saga>> ListarAsync(int pagina, int tamanhoPagina)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM saga 
            ORDER BY data_criacao DESC 
            LIMIT @Limit OFFSET @Offset";
        
        var sagas = await connection.QueryAsync<Saga>(sql, new 
        { 
            Limit = tamanhoPagina, 
            Offset = (pagina - 1) * tamanhoPagina 
        });
        
        return sagas.ToList();
    }

    public async Task<Guid> InserirAsync(Saga saga)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sqlSaga = @"
            INSERT INTO saga (id, tipo_saga, estado_saga, data_criacao, data_atualizacao, 
                              data_finalizacao, dados_contexto, mensagem_erro)
            VALUES (@Id, @TipoSaga, @EstadoSaga, @DataCriacao, @DataAtualizacao, 
                    @DataFinalizacao, @DadosContexto, @MensagemErro)";

        await connection.ExecuteAsync(sqlSaga, saga);

        if (saga.Etapas.Any())
        {
            const string sqlEtapa = @"
                INSERT INTO etapa_saga (id, saga_id, nome_etapa, ordem_execucao, estado_etapa, 
                                        tipo_acao, data_inicio, data_finalizacao, dados_entrada, 
                                        dados_saida, mensagem_erro, tentativas)
                VALUES (@Id, @SagaId, @NomeEtapa, @OrdemExecucao, @EstadoEtapa, 
                        @TipoAcao, @DataInicio, @DataFinalizacao, @DadosEntrada, 
                        @DadosSaida, @MensagemErro, @Tentativas)";
            
            await connection.ExecuteAsync(sqlEtapa, saga.Etapas);
        }

        return saga.Id;
    }

    public async Task AtualizarAsync(Saga saga)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            UPDATE saga 
            SET estado_saga = @EstadoSaga, 
                data_atualizacao = @DataAtualizacao, 
                data_finalizacao = @DataFinalizacao,
                mensagem_erro = @MensagemErro
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, saga);

        // Atualizar etapas
        if (saga.Etapas.Any())
        {
            const string sqlEtapa = @"
                UPDATE etapa_saga 
                SET estado_etapa = @EstadoEtapa,
                    data_inicio = @DataInicio,
                    data_finalizacao = @DataFinalizacao,
                    dados_saida = @DadosSaida,
                    mensagem_erro = @MensagemErro,
                    tentativas = @Tentativas
                WHERE id = @Id";
            
            await connection.ExecuteAsync(sqlEtapa, saga.Etapas);
        }
    }
}

