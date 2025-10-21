using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoConsolidarMtm : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoConsolidarMtm> _logger;

    public AcaoConsolidarMtm(CarteiraDbContext context, ILogger<AcaoConsolidarMtm> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosConsolidacao>(etapa.DadosEntrada ?? "{}");
            
            var mtm = new Mtm
            {
                Id = Guid.NewGuid(),
                CoeId = dados!.CoeId,
                DataReferencia = dados.DataReferencia,
                ValorRendaFixa = dados.ValorRendaFixa,
                ValorRendaVariavel = dados.ValorRendaVariavel,
                ValorTotal = (dados.ValorRendaFixa ?? 0) + (dados.ValorRendaVariavel ?? 0),
                DataCriacao = DateTime.UtcNow
            };

            var coe = await _context.Coes.FindAsync(dados.CoeId);
            if (coe != null)
            {
                mtm.PercentualRentabilidade = ((mtm.ValorTotal - coe.ValorNominal) / coe.ValorNominal) * 100;
            }

            _context.Mtms.Add(mtm);
            await _context.SaveChangesAsync();

            _logger.LogInformation("MTM consolidado: {MtmId} com valor total {ValorTotal}", 
                mtm.Id, mtm.ValorTotal);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { MtmId = mtm.Id, ValorTotal = mtm.ValorTotal })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consolidar MTM");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, object>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("MtmId", out var mtmIdObj))
            {
                var mtmId = Guid.Parse(mtmIdObj.ToString()!);
                var mtm = await _context.Mtms.FindAsync(mtmId);
                
                if (mtm != null)
                {
                    _context.Mtms.Remove(mtm);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("MTM {MtmId} removido na compensação", mtmId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar consolidação de MTM");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosConsolidacao
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
        public decimal? ValorRendaFixa { get; set; }
        public decimal? ValorRendaVariavel { get; set; }
    }
}

