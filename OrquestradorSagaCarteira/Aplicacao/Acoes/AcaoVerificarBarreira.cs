using System.Text.Json;
using Calculadora.Core.Services;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se uma barreira foi atingida
/// </summary>
public class AcaoVerificarBarreira : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly ICotacaoRepository _cotacaoRepository;
    private readonly IOperacaoRepository _operacaoRepository;
    private readonly CalculadoraBarreira _calculadora;
    private readonly ILogger<AcaoVerificarBarreira> _logger;

    public AcaoVerificarBarreira(
        IBarreiraRepository barreiraRepository,
        ICotacaoRepository cotacaoRepository,
        IOperacaoRepository operacaoRepository,
        CalculadoraBarreira calculadora,
        ILogger<AcaoVerificarBarreira> logger)
    {
        _barreiraRepository = barreiraRepository;
        _cotacaoRepository = cotacaoRepository;
        _operacaoRepository = operacaoRepository;
        _calculadora = calculadora;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosVerificacaoBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            _logger.LogInformation("🔍 Verificando barreira - Ticker: {Ticker}, BarreiraId: {BarreiraId}", 
                dados.Ticker, dados.BarreiraId);

            // Estratégia de busca da barreira:
            // 1. Por ID (se vier preenchido)
            // 2. Por ticker + nivel_barreira (chave composta)
            // 3. Por ticker + data (fallback)
            
            BarreiraOperacao? barreira = null;
            
            // Tentativa 1: Buscar por ID
            if (dados.BarreiraId != Guid.Empty)
            {
                barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId);
                if (barreira != null)
                    _logger.LogInformation("✓ Barreira encontrada por ID: {BarreiraId}", dados.BarreiraId);
            }

            if (barreira == null)
            {
                _logger.LogError("❌ Barreira não encontrada - Ticker: {Ticker}, Nivel: {Nivel}, Data: {Data}", 
                    dados.Ticker, dados.NivelBarreira, dados.DataReferencia);
                return new ResultadoAcao { Sucesso = false, MensagemErro = $"Barreira não encontrada para o ticker {dados.Ticker}" };
            }

            var operacao = await _operacaoRepository.ObterPorIdAsync(barreira.OperacaoId);
            if (operacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Operação não encontrada" };

            var ativoInicial = operacao.Ativos.FirstOrDefault(a => 
                a.Ticker.Equals(dados.Ticker, StringComparison.OrdinalIgnoreCase));
            if (ativoInicial == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = $"Ativo {dados.Ticker} não encontrado na operação" };

            var resultado = CalculadoraBarreira.VerificarBarreira(
                ativoInicial.CotacaoInicial,
                dados.CotacaoAtual,
                barreira.NivelBarreira,
                barreira.Condicao);

            _logger.LogInformation(
                "✔️ Barreira verificada - Ticker: {Ticker}, Atingida: {Atingida}, Taxa: {Taxa}%",
                dados.Ticker, resultado.BarreiraAtingida, resultado.TaxaVariacao);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                BarreiraAtingida = resultado.BarreiraAtingida,
                resultado.ValorObservado,
                resultado.TaxaVariacao,
                Ticker = dados.Ticker,
                BarreiraId = barreira.Id
            });

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = dadosSaida
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao verificar barreira na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
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
    public Guid BarreiraId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoAtual { get; set; }
    public DateTime DataReferencia { get; set; }
    public decimal? NivelBarreira { get; set; }
}
