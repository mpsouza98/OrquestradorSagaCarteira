using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoEncerrarCoe : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoEncerrarCoe> _logger;

    public AcaoEncerrarCoe(CarteiraDbContext context, ILogger<AcaoEncerrarCoe> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosEncerramento>(etapa.DadosEntrada ?? "{}");
            
            var coe = await _context.Coes.FindAsync(dados!.CoeId);
            if (coe == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "COE não encontrado" };

            var statusAnterior = coe.Ativo;
            coe.Ativo = false;
            coe.DataAtualizacao = DateTime.UtcNow;

            var liquidacao = await _context.Liquidacoes.FindAsync(dados.LiquidacaoId);
            if (liquidacao != null)
            {
                liquidacao.Status = "CONCLUIDA";
                liquidacao.DataProcessamento = DateTime.UtcNow;
                liquidacao.Motivo = "Autocall - Barreira atingida";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("COE {CoeId} encerrado via liquidação {LiquidacaoId}", 
                dados.CoeId, dados.LiquidacaoId);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { CoeId = coe.Id, StatusAnterior = statusAnterior })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao encerrar COE");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, object>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("CoeId", out var coeIdObj))
            {
                var coeId = Guid.Parse(coeIdObj.ToString()!);
                var coe = await _context.Coes.FindAsync(coeId);
                
                if (coe != null)
                {
                    coe.Ativo = true;
                    coe.DataAtualizacao = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("COE {CoeId} reativado na compensação", coeId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar encerramento de COE");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosEncerramento
    {
        public Guid CoeId { get; set; }
        public Guid LiquidacaoId { get; set; }
    }
}

