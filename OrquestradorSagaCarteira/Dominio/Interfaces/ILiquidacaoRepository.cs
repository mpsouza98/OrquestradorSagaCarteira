using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

public interface ILiquidacaoRepository
{
    Task<Guid> InserirAsync(LiquidacaoAgendada liquidacao);
    Task<List<LiquidacaoAgendada>> ObterAgendadasAsync();
    Task AtualizarAsync(LiquidacaoAgendada liquidacao);
}

