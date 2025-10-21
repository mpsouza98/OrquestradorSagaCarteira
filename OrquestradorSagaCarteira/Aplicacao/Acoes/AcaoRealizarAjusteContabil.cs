using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoRealizarAjusteContabil : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoRealizarAjusteContabil> _logger;

    public AcaoRealizarAjusteContabil(CarteiraDbContext context, ILogger<AcaoRealizarAjusteContabil> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            _logger.LogInformation("Ajuste contábil realizado");
            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao realizar ajuste contábil");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        _logger.LogInformation("Ajuste contábil compensado");
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

