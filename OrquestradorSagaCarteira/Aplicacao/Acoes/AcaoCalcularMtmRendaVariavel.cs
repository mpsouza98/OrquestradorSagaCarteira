using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoCalcularMtmRendaVariavel : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoCalcularMtmRendaVariavel> _logger;

    public AcaoCalcularMtmRendaVariavel(CarteiraDbContext context, ILogger<AcaoCalcularMtmRendaVariavel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosMtm>(etapa.DadosEntrada ?? "{}");
            var coe = await _context.Coes
                .Include(c => c.Ativos)
                .FirstOrDefaultAsync(c => c.Id == dados!.CoeId);

            if (coe == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "COE não encontrado" };

            decimal valorRendaVariavel = 0;

            foreach (var ativo in coe.Ativos)
            {
                var cotacao = await _context.Cotacoes
                    .Where(c => c.CodigoAtivo == ativo.CodigoAtivo && c.DataReferencia == dados.DataReferencia)
                    .FirstOrDefaultAsync();

                if (cotacao != null && ativo.Quantidade.HasValue)
                {
                    var valorAtivo = (cotacao.PrecoFechamento ?? 0) * ativo.Quantidade.Value;
                    valorRendaVariavel += valorAtivo;
                }
            }

            _logger.LogInformation("MTM Renda Variável calculado: {Valor} para COE {CoeId}", 
                valorRendaVariavel, coe.Id);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { ValorRendaVariavel = valorRendaVariavel })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular MTM Renda Variável");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }

    private class DadosMtm
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

