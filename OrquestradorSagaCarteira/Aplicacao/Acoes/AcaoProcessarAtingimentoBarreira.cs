using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoProcessarAtingimentoBarreira : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoProcessarAtingimentoBarreira> _logger;

    public AcaoProcessarAtingimentoBarreira(CarteiraDbContext context, ILogger<AcaoProcessarAtingimentoBarreira> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosAtingimento>(etapa.DadosEntrada ?? "{}");
            
            if (!dados!.BarreiraAtingida)
            {
                _logger.LogInformation("Barreira não atingida, nenhuma ação necessária");
                return new ResultadoAcao { Sucesso = true };
            }

            var barreira = await _context.Barreiras.FindAsync(dados.BarreiraId);
            if (barreira == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira não encontrada" };

            barreira.Atingida = true;
            barreira.DataAtingimento = DateTime.UtcNow;
            barreira.ValorAtingimento = dados.ValorCesta;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Barreira {BarreiraId} marcada como atingida com valor {Valor}", 
                barreira.Id, dados.ValorCesta);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { BarreiraId = barreira.Id })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar atingimento de barreira");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, string>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("BarreiraId", out var barreiraIdStr))
            {
                var barreiraId = Guid.Parse(barreiraIdStr);
                var barreira = await _context.Barreiras.FindAsync(barreiraId);
                
                if (barreira != null)
                {
                    barreira.Atingida = false;
                    barreira.DataAtingimento = null;
                    barreira.ValorAtingimento = null;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Barreira {BarreiraId} revertida na compensação", barreiraId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar atingimento de barreira");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosAtingimento
    {
        public Guid BarreiraId { get; set; }
        public bool BarreiraAtingida { get; set; }
        public decimal ValorCesta { get; set; }
    }
}

