using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoCalcularValorizacaoContabil : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoCalcularValorizacaoContabil> _logger;

    public AcaoCalcularValorizacaoContabil(CarteiraDbContext context, ILogger<AcaoCalcularValorizacaoContabil> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosValorizacao>(etapa.DadosEntrada ?? "{}");
            
            var mtm = await _context.Mtms
                .Where(m => m.CoeId == dados!.CoeId && m.DataReferencia == dados.DataReferencia)
                .FirstOrDefaultAsync();

            if (mtm == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "MTM não encontrado" };

            var coe = await _context.Coes.FindAsync(dados.CoeId);
            if (coe == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "COE não encontrado" };

            var valorizacao = new ValorizacaoContabil
            {
                Id = Guid.NewGuid(),
                CoeId = dados.CoeId,
                DataReferencia = dados.DataReferencia,
                ValorContabil = coe.ValorNominal,
                ValorMercado = mtm.ValorTotal,
                Diferenca = mtm.ValorTotal - coe.ValorNominal,
                AjusteContabil = mtm.ValorTotal - coe.ValorNominal,
                ContaDebito = mtm.ValorTotal > coe.ValorNominal ? "1.1.05.001" : "3.3.01.001",
                ContaCredito = mtm.ValorTotal > coe.ValorNominal ? "3.3.01.001" : "1.1.05.001",
                DataCriacao = DateTime.UtcNow
            };

            _context.ValorizacoesContabeis.Add(valorizacao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Valorização contábil calculada: {ValorizacaoId} com diferença {Diferenca}", 
                valorizacao.Id, valorizacao.Diferenca);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { ValorizacaoId = valorizacao.Id })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular valorização contábil");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, string>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("ValorizacaoId", out var valorizacaoIdStr))
            {
                var valorizacaoId = Guid.Parse(valorizacaoIdStr);
                var valorizacao = await _context.ValorizacoesContabeis.FindAsync(valorizacaoId);
                
                if (valorizacao != null)
                {
                    _context.ValorizacoesContabeis.Remove(valorizacao);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Valorização {ValorizacaoId} removida na compensação", valorizacaoId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar valorização contábil");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosValorizacao
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

