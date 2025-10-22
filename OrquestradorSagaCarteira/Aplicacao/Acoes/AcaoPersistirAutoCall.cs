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

            // Criar evento de autocall
            var evento = new EventoAutocall
            {
                Id = Guid.NewGuid(),
                OperacaoId = dados.OperacaoId,
                AutocallAtingido = dados.AutocallAtingido,
                ValorCesta = dados.ValorCesta,
                TipoEstrutura = dados.TipoEstrutura,
                DataEvento = DateTime.UtcNow,
                DadosEvento = JsonSerializer.Serialize(new
                {
                    dados.NivelBarreira,
                    dados.Ativos
                })
            };

            await _eventoRepository.InserirAsync(evento);

            // Se autocall foi atingido, atualizar barreira
            if (dados.AutocallAtingido && dados.BarreiraId.HasValue)
            {
                var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId.Value);
                if (barreira != null)
                {
                    barreira.Atingida = true;
                    barreira.DataAtingimento = DateTime.UtcNow;
                    barreira.ValorAtingimento = dados.ValorCesta;
                    await _barreiraRepository.AtualizarAsync(barreira);
                }
            }

            _logger.LogInformation(
                "Autocall persistido - EventoId: {EventoId}, Atingido: {Atingido}, Valor: {Valor}%",
                evento.Id, dados.AutocallAtingido, dados.ValorCesta);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                EventoId = evento.Id,
                dados.OperacaoId,
                dados.AutocallAtingido
            });

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
            if (dados?.BarreiraId.HasValue == true)
            {
                var barreira = await _barreiraRepository.ObterPorIdAsync(dados.BarreiraId.Value);
                if (barreira != null)
                {
                    barreira.Atingida = false;
                    barreira.DataAtingimento = null;
                    barreira.ValorAtingimento = null;
                    await _barreiraRepository.AtualizarAsync(barreira);
                }
            }

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
}


