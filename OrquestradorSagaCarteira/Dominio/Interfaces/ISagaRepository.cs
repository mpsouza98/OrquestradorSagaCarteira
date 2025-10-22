using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface ISagaRepository
{
    Task<Saga?> ObterPorIdAsync(Guid id);
    Task<List<Saga>> ListarAsync(int pagina, int tamanhoPagina);
    Task<Guid> InserirAsync(Saga saga);
    Task AtualizarAsync(Saga saga);
}

