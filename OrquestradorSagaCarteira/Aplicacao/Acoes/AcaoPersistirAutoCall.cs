using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para persistir evento de autocall
/// </summary>
public class AcaoPersistirAutoCall : IAcaoSaga
{
    private readonly IEventoAutocallRepository _eventoRepository;
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly ILogger<AcaoPersistirAutoCall> _logger;

    public AcaoPersistirAutoCall(
        IEventoAutocallRepository eventoRepository,
        IBarreiraRepository barreiraRepository,
        ILogger<AcaoPersistirAutoCall> logger)
    {
        _eventoRepository = eventoRepository;
        _barreiraRepository = barreiraRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirAutocall>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            var eventosCriados = new List<EventoAutocallCriado>();

            foreach (var operacao in dados.OperacoesComAutocall)
            {
                try
                {
                    // Garantir OperacaoId válido: se vazio, tentar resolver via barreira de autocall
                    var operacaoId = operacao.OperacaoId;
                    if (operacaoId == Guid.Empty)
                    {
                        var barreira = await _barreiraRepository.ObterPorIdAsync(operacao.BarreiraAutocallId);
                        if (barreira != null)
                        {
                            operacaoId = barreira.OperacaoId;
                            _logger.LogWarning("⚠️ OperacaoId ausente no payload. Resgatado via BarreiraAutocallId {BarreiraId} -> OperacaoId {OperacaoId}", operacao.BarreiraAutocallId, operacaoId);
                        }
                        else
                        {
                            _logger.LogError("❌ Não foi possível resolver OperacaoId; BarreiraAutocallId {BarreiraId} não encontrada", operacao.BarreiraAutocallId);
                            continue;
                        }
                    }

                    // Criar evento de autocall
                    var evento = new EventoAutocall
                    {
                        Id = Guid.NewGuid(),
                        OperacaoId = operacaoId,
                        AutocallAtingido = true,
                        ValorCesta = operacao.NivelBarreira,
                        TipoEstrutura = operacao.TipoEstrutura,
                        DataEvento = DateTime.UtcNow,
                        DadosEvento = JsonSerializer.Serialize(new
                        {
                            operacao.TotalBarreiras,
                            operacao.BarreirasAtingidas,
                            operacao.DataVencimento,
                            BarreiraAutocallId = operacao.BarreiraAutocallId
                        })
                    };

                    await _eventoRepository.InserirAsync(evento);

                    // Atualizar barreira de autocall como atingida
                    var barreiraAutocall = await _barreiraRepository.ObterPorIdAsync(operacao.BarreiraAutocallId);
                    if (barreiraAutocall != null)
                    {
                        barreiraAutocall.Atingida = true;
                        barreiraAutocall.DataAtingimento = DateTime.UtcNow;
                        barreiraAutocall.ValorAtingimento = operacao.NivelBarreira;
                        await _barreiraRepository.AtualizarAsync(barreiraAutocall);

                        _logger.LogInformation(
                            "✅ Autocall persistido - Operação: {OperacaoId}, EventoId: {EventoId}, Barreira: {BarreiraId}",
                            operacaoId, evento.Id, operacao.BarreiraAutocallId);
                    }

                    eventosCriados.Add(new EventoAutocallCriado
                    {
                        EventoId = evento.Id,
                        OperacaoId = operacaoId,
                        BarreiraAutocallId = operacao.BarreiraAutocallId
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao persistir autocall para operação {OperacaoId}", operacao.OperacaoId);
                }
            }

            var dadosSaida = JsonSerializer.Serialize(eventosCriados);

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao persistir autocall na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPersistirAutocall>(etapa.DadosEntrada ?? "{}");
            if (dados?.BarreiraId.HasValue != true || dados.BarreiraId == null) return new ResultadoCompensacao { Sucesso = true };

            var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId.Value);
            
            if (barreira == null) return new ResultadoCompensacao { Sucesso = true };
            
            barreira.Atingida = false;
            barreira.DataAtingimento = null;
            barreira.ValorAtingimento = null;
            await _barreiraRepository.AtualizarAsync(barreira);

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar persistência de autocall");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }
}

public class DadosPersistirAutocall
{
    public Guid OperacaoId { get; set; }
    public bool AutocallAtingido { get; set; }
    public decimal ValorCesta { get; set; }
    public decimal NivelBarreira { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty;
    public Guid? BarreiraId { get; set; }
    public List<object>? Ativos { get; set; }
    public List<OperacaoComAutocall> OperacoesComAutocall { get; set; } = new List<OperacaoComAutocall>();
}

public class OperacaoComAutocall
{
    public Guid OperacaoId { get; set; }
    public Guid BarreiraAutocallId { get; set; }
    public decimal NivelBarreira { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty;
    public DateTime DataVencimento { get; set; }
    public int TotalBarreiras { get; set; }
    public int BarreirasAtingidas { get; set; }
}

public class EventoAutocallCriado
{
    public Guid EventoId { get; set; }
    public Guid OperacaoId { get; set; }
    public Guid BarreiraAutocallId { get; set; }
}
