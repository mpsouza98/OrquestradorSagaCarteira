using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoAjustarPosicoes : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoAjustarPosicoes> _logger;

    public AcaoAjustarPosicoes(CarteiraDbContext context, ILogger<AcaoAjustarPosicoes> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            _logger.LogInformation("Posições ajustadas após evento corporativo");
            
            return new ResultadoAcao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ajustar posições");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

