using System.Text.Json;
using Calculadora.Core.Models;
using Calculadora.Core.Services;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para agregar valores da cesta de ativos
/// </summary>
public class AcaoAgregarCesta : IAcaoSaga
{
    private readonly IOperacaoRepository _operacaoRepository;
    private readonly ICotacaoRepository _cotacaoRepository;
    private readonly CalculadoraAutocall _calculadora;
    private readonly ILogger<AcaoAgregarCesta> _logger;

    public AcaoAgregarCesta(
        IOperacaoRepository operacaoRepository,
        ICotacaoRepository cotacaoRepository,
        CalculadoraAutocall calculadora,
        ILogger<AcaoAgregarCesta> logger)
    {
        _operacaoRepository = operacaoRepository;
        _cotacaoRepository = cotacaoRepository;
        _calculadora = calculadora;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosAgregarCesta>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            var operacao = await _operacaoRepository.ObterPorIdAsync(dados.OperacaoId);
            if (operacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Operação não encontrada" };

            // Obter cotações atuais de todos os ativos
            var tickers = operacao.Ativos.Select(a => a.Ticker).ToList();
            var cotacoes = await _cotacaoRepository.ObterCotacoesPorTickersAsync(tickers, dados.DataReferencia);

            var ativosCesta = new List<AtivoCesta>();
            foreach (var ativo in operacao.Ativos)
            {
                var cotacao = cotacoes.FirstOrDefault(c => c.TickerAtivo == ativo.Ticker);
                if (cotacao == null)
                {
                    _logger.LogWarning("Cotação não encontrada para {Ticker} na data {Data}", 
                        ativo.Ticker, dados.DataReferencia);
                    continue;
                }

                ativosCesta.Add(new AtivoCesta
                {
                    Ticker = ativo.Ticker,
                    CotacaoInicial = ativo.CotacaoInicial,
                    CotacaoAtual = cotacao.PrecoFechamento,
                    PercentualParticipacao = ativo.PercentualParticipacao
                });
            }

            var valorCesta = _calculadora.AgregarCesta(ativosCesta, operacao.TipoEstrutura);

            _logger.LogInformation(
                "Cesta agregada - Operacao: {OperacaoId}, Estrutura: {Estrutura}, Valor: {Valor}%",
                operacao.Id, operacao.TipoEstrutura, valorCesta);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                ValorCesta = valorCesta,
                TipoEstrutura = operacao.TipoEstrutura,
                OperacaoId = operacao.Id,
                Ativos = ativosCesta.Select(a => new
                {
                    a.Ticker,
                    Performance = _calculadora.CalcularPerformance(a.CotacaoInicial, a.CotacaoAtual)
                })
            });

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao agregar cesta na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Agregação de cesta não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

public class DadosAgregarCesta
{
    public Guid OperacaoId { get; set; }
    public DateTime DataReferencia { get; set; }
}

