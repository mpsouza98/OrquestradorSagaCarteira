using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class EventoAutocallRepository : IEventoAutocallRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public EventoAutocallRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> InserirAsync(EventoAutocall evento)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            INSERT INTO evento_autocall (id, operacao_id, autocall_atingido, valor_cesta, 
                                         tipo_estrutura, data_evento, dados_evento)
            VALUES (@Id, @OperacaoId, @AutocallAtingido, @ValorCesta, 
                    @TipoEstrutura, @DataEvento, @DadosEvento)";

        await connection.ExecuteAsync(sql, evento);
        return evento.Id;
    }

    public async Task<List<EventoAutocall>> ObterPorOperacaoAsync(Guid operacaoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM evento_autocall 
            WHERE operacao_id = @OperacaoId 
            ORDER BY data_evento DESC";
        
        var eventos = await connection.QueryAsync<EventoAutocall>(sql, new { OperacaoId = operacaoId });
        return eventos.ToList();
    }
}

