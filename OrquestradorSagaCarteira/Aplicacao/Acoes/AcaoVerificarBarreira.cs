using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoVerificarBarreira : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoVerificarBarreira> _logger;

    public AcaoVerificarBarreira(CarteiraDbContext context, ILogger<AcaoVerificarBarreira> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosBarreira>(etapa.DadosEntrada ?? "{}");
            
            var barreira = await _context.Barreiras
                .Include(b => b.Coe)
                .ThenInclude(c => c.Ativos)
                .FirstOrDefaultAsync(b => b.Id == dados!.BarreiraId);

            if (barreira == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira não encontrada" };

            // Obter cotações dos ativos na data de observação
            var tickersAtivos = barreira.Coe.Ativos.Select(a => a.TickerAtivo).ToList();
            var cotacoes = await _context.Cotacoes
                .Where(c => tickersAtivos.Contains(c.TickerAtivo) && c.Data == barreira.DataObservacao)
                .ToListAsync();

            decimal valorCesta = 0;
            foreach (var ativo in barreira.Coe.Ativos)
            {
                var cotacao = cotacoes.FirstOrDefault(c => c.TickerAtivo == ativo.TickerAtivo);
                if (cotacao != null && ativo.Quantidade.HasValue)
                {
                    valorCesta += cotacao.PrecoFechamento * ativo.Quantidade.Value;
                }
            }

            bool barreiraAtingida = false;
            
            // Verificar tipo de barreira
            switch (barreira.TipoBarreira.ToString())
            {
                case "Autocall":
                case "BestOf":
                    barreiraAtingida = valorCesta >= barreira.NivelBarreira;
                    break;
                case "WorstOf":
                    barreiraAtingida = valorCesta <= barreira.NivelBarreira;
                    break;
            }

            _logger.LogInformation("Barreira {BarreiraId} verificada: Valor {Valor}, Nível {Nivel}, Atingida: {Atingida}", 
                barreira.Id, valorCesta, barreira.NivelBarreira, barreiraAtingida);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new 
                { 
                    BarreiraAtingida = barreiraAtingida,
                    ValorCesta = valorCesta
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar barreira");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }

    private class DadosBarreira
    {
        public Guid BarreiraId { get; set; }
    }
}
