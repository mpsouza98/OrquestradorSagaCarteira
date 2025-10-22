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

            var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId);
            if (barreira == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira não encontrada" };

            var operacao = await _operacaoRepository.ObterPorIdAsync(barreira.OperacaoId);
            if (operacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Operação não encontrada" };

            // Obter cotação inicial do ativo
            var ativoInicial = operacao.Ativos.FirstOrDefault(a => a.Ticker == dados.Ticker);
            if (ativoInicial == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = $"Ativo {dados.Ticker} não encontrado na operação" };

            // Verificar barreira
            var resultado = _calculadora.VerificarBarreira(
                ativoInicial.CotacaoInicial,
                dados.CotacaoAtual,
                barreira.NivelBarreira,
                barreira.Condicao);

            _logger.LogInformation(
                "Barreira verificada - Ticker: {Ticker}, Atingida: {Atingida}, Taxa: {Taxa}%",
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
            _logger.LogError(ex, "Erro ao verificar barreira na etapa {EtapaId}", etapa.Id);
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
}
