using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface IOperacaoRepository
{
    Task<Operacao?> ObterPorIdAsync(Guid id);
    Task<Operacao?> ObterPorCodigoAsync(string codigo);
    Task<List<Operacao>> ListarAtivasAsync();
    Task<Guid> InserirAsync(Operacao operacao);
    Task AtualizarAsync(Operacao operacao);
}

