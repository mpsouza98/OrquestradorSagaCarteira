using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface IEventoAutocallRepository
{
    Task<Guid> InserirAsync(EventoAutocall evento);
    Task<List<EventoAutocall>> ObterPorOperacaoAsync(Guid operacaoId);
}

