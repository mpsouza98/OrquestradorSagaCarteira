using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface IEventoBarreiraRepository
{
    Task<Guid> InserirAsync(EventoBarreira evento);
    Task<List<EventoBarreira>> ObterPorOperacaoAsync(Guid operacaoId);
}

