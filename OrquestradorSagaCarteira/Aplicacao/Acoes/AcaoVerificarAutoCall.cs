using System.Text.Json;
using Calculadora.Core.Services;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para verificar se autocall foi atingido (BestOf ou WorstOf)
/// </summary>
public class AcaoVerificarAutoCall : IAcaoSaga
{
    private readonly IBarreiraRepository _barreiraRepository;
    private readonly CalculadoraAutocall _calculadora;
    private readonly ILogger<AcaoVerificarAutoCall> _logger;

    public AcaoVerificarAutoCall(
        IBarreiraRepository barreiraRepository,
        CalculadoraAutocall calculadora,
        ILogger<AcaoVerificarAutoCall> logger)
    {
        _barreiraRepository = barreiraRepository;
        _calculadora = calculadora;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosVerificarAutocall>(etapa.DadosEntrada ?? "{}");
            if (dados == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Dados de entrada inválidos" };

            // Buscar barreira de autocall da operação
            var barreiras = await _barreiraRepository.ObterPorOperacaoAsync(dados.OperacaoId);
            var barreiraAutocall = barreiras.FirstOrDefault(b => 
                b.TipoBarreira == "Autocall" && b.Ativa && !b.Atingida);

            if (barreiraAutocall == null)
            {
                _logger.LogWarning("Barreira de autocall não encontrada para operação {OperacaoId}", dados.OperacaoId);
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Barreira de autocall não encontrada" };
            }

            // Verificar se autocall foi atingido
            var autocallAtingido = dados.ValorCesta >= barreiraAutocall.NivelBarreira;

            _logger.LogInformation(
                "Autocall verificado - Operacao: {OperacaoId}, Atingido: {Atingido}, Valor: {Valor}%, Barreira: {Barreira}%",
                dados.OperacaoId, autocallAtingido, dados.ValorCesta, barreiraAutocall.NivelBarreira);

            var dadosSaida = JsonSerializer.Serialize(new
            {
                AutocallAtingido = autocallAtingido,
                ValorCesta = dados.ValorCesta,
                NivelBarreira = barreiraAutocall.NivelBarreira,
                TipoEstrutura = dados.TipoEstrutura,
                BarreiraId = barreiraAutocall.Id,
                OperacaoId = dados.OperacaoId,
                dados.Ativos
            });

            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar autocall na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        // Verificação de autocall não requer compensação
        return await Task.FromResult(new ResultadoCompensacao { Sucesso = true });
    }
}

public class DadosVerificarAutocall
{
    public Guid OperacaoId { get; set; }
    public decimal ValorCesta { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty;
    public List<object>? Ativos { get; set; }
}

