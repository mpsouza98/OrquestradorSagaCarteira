using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface IBarreiraRepository
{
    Task<BarreiraOperacao?> ObterPorIdAsync(Guid id);
    Task<List<BarreiraOperacao>> ObterPorOperacaoAsync(Guid operacaoId);
    Task<List<BarreiraOperacao>> ObterBarreirasAtivasPorTickerAsync(string ticker);
    Task<Guid> InserirAsync(BarreiraOperacao barreira);
    Task AtualizarAsync(BarreiraOperacao barreira);
}
