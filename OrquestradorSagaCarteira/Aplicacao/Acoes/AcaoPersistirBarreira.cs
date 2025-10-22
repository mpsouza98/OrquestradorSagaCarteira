using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para persistir evento de barreira atingida
/// </summary>
public class AcaoPersistirBarreira : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly IEventoBarreiraRepository _eventoRepository;
    private readonly ILogger<AcaoPersistirBarreira> _logger;

    public AcaoPersistirBarreira(
        IBarreiraRepository barreiraRepository,
        IEventoBarreiraRepository eventoRepository,
        ILogger<AcaoPersistirBarreira> logger)
    {
        _barreiraRepository = barreiraRepository;
        _eventoRepository = eventoRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || !dados.BarreiraAtingida)
                return new ResultadoAcao { Sucesso = true, DadosSaida = "{}" };

            var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId);
            if (barreira == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira não encontrada" };

            // Atualizar barreira
            barreira.Atingida = true;
            barreira.DataAtingimento = DateTime.UtcNow;
            barreira.ValorAtingimento = dados.ValorObservado;
            await _barreiraRepository.AtualizarAsync(barreira);

            // Criar evento de barreira
            var evento = new EventoBarreira
            {
                Id = Guid.NewGuid(),
                BarreiraId = barreira.Id,
                OperacaoId = barreira.OperacaoId,
                Ticker = dados.Ticker,
                ValorObservado = dados.ValorObservado,
                NivelBarreira = barreira.NivelBarreira,
                TipoBarreira = barreira.TipoBarreira,
                DataEvento = DateTime.UtcNow,
                DadosEvento = JsonSerializer.Serialize(new { dados.TaxaVariacao, barreira.Condicao })
            };

            await _eventoRepository.InserirAsync(evento);

            _logger.LogInformation(
                "Barreira persistida - ID: {BarreiraId}, Ticker: {Ticker}, Valor: {Valor}",
                barreira.Id, dados.Ticker, dados.ValorObservado);

            var dadosSaida = JsonSerializer.Serialize(new { EventoId = evento.Id, barreira.OperacaoId });
            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao persistir barreira na etapa {EtapaId}", etapa.Id);
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

