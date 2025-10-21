using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação removida - Cotações agora são pré-inseridas na base de dados
/// </summary>
public class AcaoProcessarCotacao : IAcaoSaga
{
    private readonly ILogger<AcaoProcessarCotacao> _logger;

    public AcaoProcessarCotacao(ILogger<AcaoProcessarCotacao> logger)
    {
        _logger = logger;
    }

    public Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        _logger.LogInformation("Ação de processamento de cotação descontinuada - Cotações disponíveis na base");
        return Task.FromResult(new ResultadoAcao { Sucesso = true });
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}
