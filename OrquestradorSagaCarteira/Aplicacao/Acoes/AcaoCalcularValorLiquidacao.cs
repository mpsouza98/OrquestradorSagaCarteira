using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoCalcularValorLiquidacao : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoCalcularValorLiquidacao> _logger;

    public AcaoCalcularValorLiquidacao(CarteiraDbContext context, ILogger<AcaoCalcularValorLiquidacao> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosCalculo>(etapa.DadosEntrada ?? "{}");
            
            var liquidacao = await _context.Liquidacoes
                .Include(l => l.Coe)
                .FirstOrDefaultAsync(l => l.Id == dados!.LiquidacaoId);

            if (liquidacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Liquidação não encontrada" };

            // Buscar último MTM
            var mtm = await _context.Mtms
                .Where(m => m.CoeId == liquidacao.CoeId)
                .OrderByDescending(m => m.DataReferencia)
                .FirstOrDefaultAsync();

            decimal valorLiquidacao = mtm?.ValorTotal ?? liquidacao.Coe.ValorNominal;
            decimal percentualRetorno = mtm?.PercentualRentabilidade ?? 0;

            liquidacao.ValorLiquidacao = valorLiquidacao;
            liquidacao.PercentualRetorno = percentualRetorno;
            liquidacao.Status = "CALCULADA";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Valor de liquidação calculado: {Valor} ({Percentual}%)", 
                valorLiquidacao, percentualRetorno);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new 
                { 
                    LiquidacaoId = liquidacao.Id,
                    ValorLiquidacao = valorLiquidacao
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular valor de liquidação");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, object>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("LiquidacaoId", out var liquidacaoIdObj))
            {
                var liquidacaoId = Guid.Parse(liquidacaoIdObj.ToString()!);
                var liquidacao = await _context.Liquidacoes.FindAsync(liquidacaoId);
                
                if (liquidacao != null)
                {
                    liquidacao.ValorLiquidacao = 0;
                    liquidacao.PercentualRetorno = null;
                    liquidacao.Status = "INICIADA";
                    await _context.SaveChangesAsync();
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar cálculo de liquidação");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosCalculo
    {
        public Guid LiquidacaoId { get; set; }
    }
}

