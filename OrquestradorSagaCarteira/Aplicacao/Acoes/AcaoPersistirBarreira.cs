using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para validar persistência de barreira (já persistida pelo Observer)
/// </summary>
public class AcaoPersistirBarreira : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly ILogger<AcaoPersistirBarreira> _logger;

    public AcaoPersistirBarreira(
        IBarreiraRepository barreiraRepository,
        ILogger<AcaoPersistirBarreira> logger)
    {
        _barreiraRepository = barreiraRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || !dados.BarreiraAtingida)
                return new ResultadoAcao { Sucesso = true, DadosSaida = "{}" };

            // Validar se a barreira foi persistida corretamente
            var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId);
            if (barreira == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira não encontrada para validação" };

            if (!barreira.Atingida)
            {
                _logger.LogWarning("⚠️ Barreira {BarreiraId} não estava marcada como atingida, atualizando...", dados.BarreiraId);

                barreira.Atingida = true;
                barreira.DataAtingimento = DateTime.UtcNow;
                barreira.ValorAtingimento = dados.ValorObservado;
                await _barreiraRepository.AtualizarAsync(barreira);
            }

            _logger.LogInformation(
                "✅ Barreira validada - ID: {BarreiraId}, Ticker: {Ticker}, Atingida: {Atingida}",
                barreira.Id, dados.Ticker, barreira.Atingida);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                BarreiraId = barreira.Id,
                OperacaoId = barreira.OperacaoId,
                dados.Ticker,
                Validada = true
            });

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar persistência de barreira na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoCompensacao { Sucesso = true };

            var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId);
            if (barreira != null)
            {
                barreira.Atingida = false;
                barreira.DataAtingimento = null;
                barreira.ValorAtingimento = null;
                await _barreiraRepository.AtualizarAsync(barreira);

                _logger.LogInformation("↩️ Barreira {BarreiraId} compensada (resetada)", dados.BarreiraId);
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar persistência de barreira");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }
}

public class DadosPersistirBarreira
{
    public Guid BarreiraId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public bool BarreiraAtingida { get; set; }
    public decimal ValorObservado { get; set; }
    public decimal TaxaVariacao { get; set; }
}
