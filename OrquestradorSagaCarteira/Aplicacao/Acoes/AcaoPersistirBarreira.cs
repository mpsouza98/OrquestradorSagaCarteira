using System.Text.Json;
using Calculadora.Core.Models;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para persistir barreiras atingidas em lote
/// </summary>
public class AcaoPersistirBarreira : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly IEventoBarreiraRepository _eventoBarreiraRepository;
    private readonly ILogger<AcaoPersistirBarreira> _logger;

    public AcaoPersistirBarreira(
        IBarreiraRepository barreiraRepository,
        IEventoBarreiraRepository eventoBarreiraRepository,
        ILogger<AcaoPersistirBarreira> logger)
    {
        _barreiraRepository = barreiraRepository;
        _eventoBarreiraRepository = eventoBarreiraRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || dados.BarreirasAtingidas == null || !dados.BarreirasAtingidas.Any())
            {
                _logger.LogInformation("ℹ️ Nenhuma barreira atingida para persistir");
                return new ResultadoAcao { Sucesso = true, DadosSaida = "{}" };
            }

            // Obter fonte do contexto da saga
            var fonte = "";
            if (etapa.Saga != null && !string.IsNullOrEmpty(etapa.Saga.DadosContexto))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(etapa.Saga.DadosContexto);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Fonte", out var fonteElement))
                    {
                        fonte = fonteElement.GetString() ?? "";
                    }
                }
                catch { /* Ignora erro ao buscar fonte */ }
            }

            _logger.LogInformation("💾 Persistindo {Qtd} barreiras atingidas em lote", dados.BarreirasAtingidas.Count);

            var barreiraIds = dados.BarreirasAtingidas.Select(b => b.BarreiraId).ToList();
            var eventosBarreira = new List<EventoBarreira>();

            // Buscar todas as barreiras de uma vez (evitar N+1)
            var barreiras = new List<BarreiraOperacao>();
            foreach (var barreiraId in barreiraIds)
            {
                var barreira = await _barreiraRepository.ObterPorIdAsync(barreiraId);
                if (barreira != null)
                    barreiras.Add(barreira);
            }

            // Atualizar todas as barreiras
            foreach (var resultado in dados.BarreirasAtingidas)
            {
                var barreira = barreiras.FirstOrDefault(b => b.Id == resultado.BarreiraId);
                if (barreira == null)
                {
                    _logger.LogWarning("⚠️ Barreira {BarreiraId} não encontrada", resultado.BarreiraId);
                    continue;
                }

                // Atualizar estado da barreira
                barreira.Atingida = true;
                barreira.DataAtingimento = DateTime.UtcNow;
                barreira.ValorAtingimento = resultado.ValorObservado;
                await _barreiraRepository.AtualizarAsync(barreira);

                // Criar evento de barreira atingida
                var eventoBarreira = new EventoBarreira
                {
                    Id = Guid.NewGuid(),
                    BarreiraId = barreira.Id,
                    OperacaoId = barreira.OperacaoId,
                    Ticker = barreira.Ticker,
                    ValorObservado = resultado.ValorObservado,
                    NivelBarreira = resultado.NivelBarreira,
                    TipoBarreira = barreira.TipoBarreira,
                    DataEvento = DateTime.UtcNow,
                    DadosEvento = JsonSerializer.Serialize(new
                    {
                        CotacaoInicial = resultado.NivelBarreira,
                        CotacaoAtual = resultado.ValorObservado,
                        resultado.TaxaVariacao,
                        resultado.Condicao,
                        Fonte = fonte
                    })
                };

                await _eventoBarreiraRepository.InserirAsync(eventoBarreira);
                eventosBarreira.Add(eventoBarreira);

                _logger.LogInformation(
                    "✅ Barreira {BarreiraId} persistida - OperacaoId: {OperacaoId}, Ticker: {Ticker}, Valor: {Valor}",
                    barreira.Id, barreira.OperacaoId, barreira.Ticker, resultado.ValorObservado);
            }

            var dadosSaida = JsonSerializer.Serialize(new
            {
                TotalPersistidas = eventosBarreira.Count,
                Eventos = eventosBarreira.Select(e => new
                {
                    EventoId = e.Id,
                    e.BarreiraId,
                    e.OperacaoId,
                    e.Ticker
                }).ToList()
            });

            _logger.LogInformation("✔️ Persistência em lote concluída - {Qtd} barreiras atualizadas", eventosBarreira.Count);

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao persistir barreiras na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirBarreira>(etapa.DadosEntrada ?? "{}");
            if (dados == null || dados.BarreirasAtingidas == null)
                return new ResultadoCompensacao { Sucesso = true };

            _logger.LogWarning("↩️ Compensando persistência de {Qtd} barreiras", dados.BarreirasAtingidas.Count);

            foreach (var resultado in dados.BarreirasAtingidas)
            {
                var barreira = await _barreiraRepository.ObterPorIdAsync(resultado.BarreiraId);
                if (barreira != null)
                {
                    barreira.Atingida = false;
                    barreira.DataAtingimento = null;
                    barreira.ValorAtingimento = null;
                    await _barreiraRepository.AtualizarAsync(barreira);
                }
            }

            _logger.LogInformation("↩️ Compensação concluída");
            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao compensar persistência de barreiras");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }
}

public class DadosPersistirBarreira
{
    public List<ResultadoVerificacaoBarreira> BarreirasAtingidas { get; set; } = new();
    public string Fonte { get; set; } = string.Empty;
}
