using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoLancarContabilidade : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoLancarContabilidade> _logger;

    public AcaoLancarContabilidade(CarteiraDbContext context, ILogger<AcaoLancarContabilidade> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            _logger.LogInformation("Lançamento contábil realizado");
            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao lançar contabilidade");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        _logger.LogInformation("Lançamento contábil compensado");
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

