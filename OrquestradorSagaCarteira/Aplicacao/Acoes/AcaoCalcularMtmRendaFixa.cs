using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoCalcularMtmRendaFixa : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoCalcularMtmRendaFixa> _logger;

    public AcaoCalcularMtmRendaFixa(CarteiraDbContext context, ILogger<AcaoCalcularMtmRendaFixa> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosMtm>(etapa.DadosEntrada ?? "{}");
            var coe = await _context.Coes.FindAsync(dados!.CoeId);

            if (coe == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "COE não encontrado" };

            // Cálculo simplificado do MTM de renda fixa
            var valorRendaFixa = coe.ValorNominal * (coe.PercentualRendaFixa / 100);
            var diasCorridos = (dados.DataReferencia - coe.DataEmissao).Days;
            var taxaDiaria = 0.0001m; // Taxa simplificada
            var valorMtmRendaFixa = valorRendaFixa * (1 + taxaDiaria * diasCorridos);

            _logger.LogInformation("MTM Renda Fixa calculado: {Valor} para COE {CoeId}", 
                valorMtmRendaFixa, coe.Id);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { ValorRendaFixa = valorMtmRendaFixa })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular MTM Renda Fixa");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Compensação não necessária - cálculo não persiste dados
        return Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }

    private class DadosMtm
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

