using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação removida - MTM agora é pré-calculado e disponível na base de dados
/// </summary>
public class AcaoConsolidarMtm : IAcaoSaga
{
    private readonly ILogger<AcaoConsolidarMtm> _logger;

    public AcaoConsolidarMtm(ILogger<AcaoConsolidarMtm> logger)
    {
        _logger = logger;
    }

    public Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        _logger.LogInformation("Ação de consolidação MTM descontinuada - MTM disponível na base");
        return Task.FromResult(new ResultadoAcao { Sucesso = true });
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}
