using System.Text.Json;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

/// <summary>
/// Ação para agendar desfazimento de operação (liquidação)
/// </summary>
public class AcaoAgendarDesfazimentoOperacao : IAcaoSaga
{
    private readonly ILiquidacaoRepository _liquidacaoRepository;
    private readonly IOperacaoRepository _operacaoRepository;
    private readonly ILogger<AcaoAgendarDesfazimentoOperacao> _logger;

    public AcaoAgendarDesfazimentoOperacao(
        ILiquidacaoRepository liquidacaoRepository,
        IOperacaoRepository operacaoRepository,
        ILogger<AcaoAgendarDesfazimentoOperacao> logger)
    {
        _liquidacaoRepository = liquidacaoRepository;
        _operacaoRepository = operacaoRepository;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosAgendarLiquidacao>(etapa.DadosEntrada ?? "{}");
            if (dados == null || !dados.AutocallAtingido)
            {
                // Se autocall não foi atingido, não há liquidação a agendar
                return new ResultadoAcao { Sucesso = true, DadosSaida = "{}" };
            }

            var operacao = await _operacaoRepository.ObterPorIdAsync(dados.OperacaoId);
            if (operacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Operação não encontrada" };

            // Calcular valor de liquidação
            var valorLiquidacao = operacao.ValorNominal * (1 + dados.ValorCesta / 100);

            var liquidacao = new LiquidacaoAgendada
            {
                Id = Guid.NewGuid(),
                OperacaoId = operacao.Id,
                DataAgendamento = DateTime.UtcNow,
                DataLiquidacao = dados.DataLiquidacao,
                ValorLiquidacao = valorLiquidacao,
                Status = "Agendada",
                Motivo = "Autocall atingido",
                DadosLiquidacao = JsonSerializer.Serialize(new
                {
                    dados.ValorCesta,
                    TaxaRetorno = dados.ValorCesta,
                    operacao.TipoEstrutura
                })
            };

            await _liquidacaoRepository.InserirAsync(liquidacao);

            // Desativar operação
            operacao.Ativa = false;
            operacao.DataAtualizacao = DateTime.UtcNow;
            await _operacaoRepository.AtualizarAsync(operacao);

            _logger.LogInformation(
                "Liquidação agendada - ID: {LiquidacaoId}, Operacao: {OperacaoId}, Valor: {Valor}, Data: {Data}",
                liquidacao.Id, operacao.Id, valorLiquidacao, dados.DataLiquidacao);

            var dadosSaida = JsonSerializer.Serialize(new { LiquidacaoId = liquidacao.Id });
            return new ResultadoAcao { Sucesso = true, DadosSaida = dadosSaida };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao agendar liquidação na etapa {EtapaId}", etapa.Id);
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<dynamic>(etapa.DadosSaida ?? "{}");
            if (dadosSaida?.LiquidacaoId != null)
            {
                var liquidacaoId = Guid.Parse(dadosSaida.LiquidacaoId.ToString());
                var liquidacoes = await _liquidacaoRepository.ObterAgendadasAsync();
                var liquidacao = liquidacoes.FirstOrDefault(l => l.Id == liquidacaoId);
                
                if (liquidacao != null)
                {
                    liquidacao.Status = "Cancelada";
                    liquidacao.DataProcessamento = DateTime.UtcNow;
                    await _liquidacaoRepository.AtualizarAsync(liquidacao);

                    // Reativar operação
                    var operacao = await _operacaoRepository.ObterPorIdAsync(liquidacao.OperacaoId);
                    if (operacao != null)
                    {
                        operacao.Ativa = true;
                        operacao.DataAtualizacao = DateTime.UtcNow;
                        await _operacaoRepository.AtualizarAsync(operacao);
                    }
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar agendamento de liquidação");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }
}

public class DadosAgendarLiquidacao
{
    public Guid OperacaoId { get; set; }
    public bool AutocallAtingido { get; set; }
    public decimal ValorCesta { get; set; }
    public DateTime DataLiquidacao { get; set; }
}

