using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoIniciarLiquidacao : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoIniciarLiquidacao> _logger;

    public AcaoIniciarLiquidacao(CarteiraDbContext context, ILogger<AcaoIniciarLiquidacao> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosLiquidacao>(etapa.DadosEntrada ?? "{}");
            
            var liquidacao = new Liquidacao
            {
                Id = Guid.NewGuid(),
                CoeId = dados!.CoeId,
                BarreiraId = dados.BarreiraId,
                TipoLiquidacao = "AUTOCALL",
                DataLiquidacao = dados.DataLiquidacao,
                ValorLiquidacao = 0, // Será calculado na próxima etapa
                Status = "INICIADA",
                DataCriacao = DateTime.UtcNow
            };

            _context.Liquidacoes.Add(liquidacao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Liquidação {LiquidacaoId} iniciada para COE {CoeId}", 
                liquidacao.Id, dados.CoeId);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { LiquidacaoId = liquidacao.Id })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar liquidação");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, string>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("LiquidacaoId", out var liquidacaoIdStr))
            {
                var liquidacaoId = Guid.Parse(liquidacaoIdStr);
                var liquidacao = await _context.Liquidacoes.FindAsync(liquidacaoId);
                
                if (liquidacao != null)
                {
                    _context.Liquidacoes.Remove(liquidacao);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Liquidação {LiquidacaoId} removida na compensação", liquidacaoId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar início de liquidação");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosLiquidacao
    {
        public Guid CoeId { get; set; }
        public Guid? BarreiraId { get; set; }
        public DateTime DataLiquidacao { get; set; }
    }
}

