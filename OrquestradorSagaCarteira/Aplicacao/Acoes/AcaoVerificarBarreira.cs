using System.Text.Json;
using Calculadora.Core.Models;
using Calculadora.Core.Services;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se barreiras foram atingidas (processamento em lote)
/// Lê os dados das barreiras do DadosContexto da saga
/// </summary>
public class AcaoVerificarBarreira : IAcaoSaga
{
    private readonly ILogger<AcaoVerificarBarreira> _logger;

    public AcaoVerificarBarreira(
        ILogger<AcaoVerificarBarreira> logger)
    {
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            // Obter dados do contexto da saga ao invés de DadosEntrada
            if (etapa.Saga == null || string.IsNullOrEmpty(etapa.Saga.DadosContexto))
            {
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Contexto da saga não disponível" };
            }

            var contexto = JsonSerializer.Deserialize<ContextoBarreira>(etapa.Saga.DadosContexto);
            if (contexto == null || contexto.Barreiras == null || !contexto.Barreiras.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma barreira para verificar no contexto");
                return new ResultadoAcao { Sucesso = true, DadosSaida = JsonSerializer.Serialize(new
                {
                    TotalVerificadas = 0,
                    TotalAtingidas = 0,
                    Resultados = new List<ResultadoVerificacaoBarreira>(),
                    Ticker = contexto?.Ticker ?? "",
                    CotacaoAtual = contexto?.CotacaoAtual ?? 0
                })};
            }

            _logger.LogInformation("🔍 Verificando {Qtd} barreiras em lote - Ticker: {Ticker}, Cotação: {Cotacao}", 
                contexto.Barreiras.Count, contexto.Ticker, contexto.CotacaoAtual);

            var resultados = new List<ResultadoVerificacaoBarreira>();

            foreach (var barreiraInfo in contexto.Barreiras)
            {
                var resultado = CalculadoraBarreira.VerificarBarreira(
                    barreiraInfo.NivelBarreira,
                    contexto.CotacaoAtual,
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
                    "📊 Barreira {BarreiraId} (Operacao: {OperacaoId}) - Atingida: {Atingida}, Taxa: {Taxa}%",
                    barreiraInfo.BarreiraId, barreiraInfo.OperacaoId, resultado.BarreiraAtingida, resultado.TaxaVariacao);
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
                Ticker = contexto.Ticker,
                CotacaoAtual = contexto.CotacaoAtual
            });

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = dadosSaida
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao verificar barreiras na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Verificação de barreira não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

// Classes de contexto para deserialização
public class ContextoBarreira
{
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoAtual { get; set; }
    public DateTime DataReferencia { get; set; }
    public string Fonte { get; set; } = string.Empty;
    public List<BarreiraParaVerificar> Barreiras { get; set; } = new();
}

public class BarreiraParaVerificar
{
    public Guid BarreiraId { get; set; }
    public Guid OperacaoId { get; set; }
    public decimal NivelBarreira { get; set; }
    public string Condicao { get; set; } = string.Empty;
}
