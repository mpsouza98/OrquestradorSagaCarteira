using Dapper;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia.Repositories;

public class EventoBarreiraRepository : IEventoBarreiraRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public EventoBarreiraRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> InserirAsync(EventoBarreira evento)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            INSERT INTO evento_barreira (id, barreira_id, operacao_id, ticker, valor_observado, 
                                         nivel_barreira, tipo_barreira, data_evento, dados_evento)
            VALUES (@Id, @BarreiraId, @OperacaoId, @Ticker, @ValorObservado, 
                    @NivelBarreira, @TipoBarreira, @DataEvento, @DadosEvento)";

        await connection.ExecuteAsync(sql, evento);
        return evento.Id;
    }

    public async Task<List<EventoBarreira>> ObterPorOperacaoAsync(Guid operacaoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT * FROM evento_barreira 
            WHERE operacao_id = @OperacaoId 
            ORDER BY data_evento DESC";
        
        var eventos = await connection.QueryAsync<EventoBarreira>(sql, new { OperacaoId = operacaoId });
        return eventos.ToList();
    }
}

