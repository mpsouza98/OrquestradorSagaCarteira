using System.Text.Json;
using Calculadora.Core.Models;
using Calculadora.Core.Services;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se barreiras foram atingidas (processamento em lote)
/// </summary>
public class AcaoVerificarBarreira : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly ILogger<AcaoVerificarBarreira> _logger;

    public AcaoVerificarBarreira(
        IBarreiraRepository barreiraRepository,
        ILogger<AcaoVerificarBarreira> logger)
    {
        _barreiraRepository = barreiraRepository;
        _logger = logger;
    }

    public Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosVerificacaoBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || dados.Barreiras.Count == 0)
                return Task.FromResult(new ResultadoAcao { Sucesso = false, MensagemErro = "Nenhuma barreira para verificar" });

            _logger.LogInformation("🔍 Verificando {Qtd} barreiras em lote - Cotação: {Cotacao}", 
                dados.Barreiras.Count, dados.CotacaoAtual);

            var resultados = new List<ResultadoVerificacaoBarreira>();

            foreach (var barreiraInfo in dados.Barreiras)
            {
                var resultado = CalculadoraBarreira.VerificarBarreira(
                    barreiraInfo.NivelBarreira,
                    dados.CotacaoAtual,
                    barreiraInfo.NivelBarreira,
                    barreiraInfo.Condicao);

                resultados.Add(new ResultadoVerificacaoBarreira
                {
                    BarreiraId = barreiraInfo.BarreiraId,
                    BarreiraAtingida = resultado.BarreiraAtingida,
                    ValorObservado = resultado.ValorObservado,
                    NivelBarreira = resultado.NivelBarreira,
                    TaxaVariacao = resultado.TaxaVariacao,
                    Condicao = resultado.Condicao
                });

                _logger.LogInformation(
                    "📊 Barreira {BarreiraId} - Atingida: {Atingida}, Taxa: {Taxa}%",
                    barreiraInfo.BarreiraId, resultado.BarreiraAtingida, resultado.TaxaVariacao);
            }

            var barreiraAtingidas = resultados.Where(r => r.BarreiraAtingida).ToList();
            
            _logger.LogInformation(
                "✔️ Verificação concluída - {Total} barreiras verificadas, {Atingidas} atingidas",
                resultados.Count, barreiraAtingidas.Count);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                TotalVerificadas = resultados.Count,
                TotalAtingidas = barreiraAtingidas.Count,
                Resultados = barreiraAtingidas,
                Ticker = dados.Ticker,
                CotacaoAtual = dados.CotacaoAtual
            });

            return Task.FromResult(new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = dadosSaida
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao verificar barreiras na etapa {EtapaId}", etapa.Id);
            return Task.FromResult(new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message });
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Verificação de barreira não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

public class DadosVerificacaoBarreira
{
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoAtual { get; set; }
    public DateTime DataReferencia { get; set; }
    public List<BarreiraParaVerificar> Barreiras { get; set; } = new();
}

public class BarreiraParaVerificar
{
    public Guid BarreiraId { get; set; }
    public Guid OperacaoId { get; set; }
    public decimal NivelBarreira { get; set; }
    public string Condicao { get; set; } = string.Empty;
}
