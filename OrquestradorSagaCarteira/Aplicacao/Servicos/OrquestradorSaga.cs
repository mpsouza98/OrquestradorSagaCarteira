using Microsoft.EntityFrameworkCore;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using OrquestradorSagaCarteira.Aplicacao.Acoes;

namespace OrquestradorSagaCarteira.Aplicacao.Servicos;

public class OrquestradorSaga : IOrquestradorSaga
{
    private readonly CarteiraDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrquestradorSaga> _logger;
    private readonly Dictionary<TipoAcao, Type> _mapaAcoes;

    public OrquestradorSaga(
        CarteiraDbContext context, 
        IServiceProvider serviceProvider,
        ILogger<OrquestradorSaga> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _mapaAcoes = InicializarMapaAcoes();
    }

    public async Task<Saga> IniciarSagaAsync(Saga saga)
    {
        saga.Id = Guid.NewGuid();
        saga.EstadoSaga = EstadoSaga.Iniciada;
        saga.DataCriacao = DateTime.UtcNow;
        saga.DataAtualizacao = DateTime.UtcNow;

        // Atribuir IDs e ordem às etapas
        for (int i = 0; i < saga.Etapas.Count; i++)
        {
            saga.Etapas[i].Id = Guid.NewGuid();
            saga.Etapas[i].SagaId = saga.Id;
            saga.Etapas[i].OrdemExecucao = i + 1;
            saga.Etapas[i].EstadoEtapa = EstadoEtapa.Pendente;
        }

        _context.Sagas.Add(saga);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saga {SagaId} do tipo {TipoSaga} iniciada com {NumEtapas} etapas",
            saga.Id, saga.TipoSaga, saga.Etapas.Count);

        // Iniciar execução
        await ExecutarProximaEtapaAsync(saga.Id);

        return saga;
    }

    public async Task ExecutarProximaEtapaAsync(Guid sagaId)
    {
        var saga = await _context.Sagas
            .Include(s => s.Etapas)
            .FirstOrDefaultAsync(s => s.Id == sagaId);

        if (saga == null)
        {
            _logger.LogError("Saga {SagaId} não encontrada", sagaId);
            return;
        }

        // Buscar próxima etapa pendente
        var proximaEtapa = saga.Etapas
            .Where(e => e.EstadoEtapa == EstadoEtapa.Pendente)
            .OrderBy(e => e.OrdemExecucao)
            .FirstOrDefault();

        if (proximaEtapa == null)
        {
            // Todas as etapas concluídas
            saga.EstadoSaga = EstadoSaga.Concluida;
            saga.DataFinalizacao = DateTime.UtcNow;
            saga.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saga {SagaId} concluída com sucesso", sagaId);
            return;
        }

        // Executar etapa
        proximaEtapa.EstadoEtapa = EstadoEtapa.EmExecucao;
        proximaEtapa.DataInicio = DateTime.UtcNow;
        saga.EstadoSaga = EstadoSaga.EmExecucao;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        try
        {
            var acao = ObterAcao(proximaEtapa.TipoAcao);
            var resultado = await acao.ExecutarAsync(proximaEtapa);

            if (resultado.Sucesso)
            {
                proximaEtapa.EstadoEtapa = EstadoEtapa.Concluida;
                proximaEtapa.DataFinalizacao = DateTime.UtcNow;
                proximaEtapa.DadosSaida = resultado.DadosSaida;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Etapa {EtapaId} da Saga {SagaId} concluída com sucesso",
                    proximaEtapa.Id, sagaId);

                // Executar próxima etapa
                await ExecutarProximaEtapaAsync(sagaId);
            }
            else
            {
                // Falha na etapa - iniciar compensação
                proximaEtapa.EstadoEtapa = EstadoEtapa.Falhou;
                proximaEtapa.DataFinalizacao = DateTime.UtcNow;
                proximaEtapa.MensagemErro = resultado.MensagemErro;
                proximaEtapa.Tentativas++;
                await _context.SaveChangesAsync();

                _logger.LogError("Etapa {EtapaId} da Saga {SagaId} falhou: {Erro}",
                    proximaEtapa.Id, sagaId, resultado.MensagemErro);

                await CompensarSagaAsync(sagaId);
            }
        }
        catch (Exception ex)
        {
            proximaEtapa.EstadoEtapa = EstadoEtapa.Falhou;
            proximaEtapa.DataFinalizacao = DateTime.UtcNow;
            proximaEtapa.MensagemErro = ex.Message;
            proximaEtapa.Tentativas++;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Erro ao executar etapa {EtapaId} da Saga {SagaId}",
                proximaEtapa.Id, sagaId);

            await CompensarSagaAsync(sagaId);
        }
    }

    public async Task CompensarSagaAsync(Guid sagaId)
    {
        var saga = await _context.Sagas
            .Include(s => s.Etapas)
            .ThenInclude(e => e.Compensacoes)
            .FirstOrDefaultAsync(s => s.Id == sagaId);

        if (saga == null)
        {
            _logger.LogError("Saga {SagaId} não encontrada para compensação", sagaId);
            return;
        }

        saga.EstadoSaga = EstadoSaga.Compensando;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogWarning("Iniciando compensação da Saga {SagaId}", sagaId);

        // Compensar etapas na ordem inversa
        var etapasParaCompensar = saga.Etapas
            .Where(e => e.EstadoEtapa == EstadoEtapa.Concluida)
            .OrderByDescending(e => e.OrdemExecucao)
            .ToList();

        foreach (var etapa in etapasParaCompensar)
        {
            etapa.EstadoEtapa = EstadoEtapa.Compensando;
            await _context.SaveChangesAsync();

            try
            {
                var acao = ObterAcao(etapa.TipoAcao);
                var resultado = await acao.CompensarAsync(etapa);

                var compensacao = new HistoricoCompensacao
                {
                    Id = Guid.NewGuid(),
                    EtapaSagaId = etapa.Id,
                    TipoCompensacao = etapa.TipoAcao.ToString(),
                    EstadoCompensacao = resultado.Sucesso ? "Concluida" : "Falhou",
                    DadosCompensacao = resultado.DadosCompensacao,
                    DataInicio = DateTime.UtcNow,
                    DataFinalizacao = DateTime.UtcNow,
                    MensagemErro = resultado.MensagemErro
                };

                _context.HistoricosCompensacao.Add(compensacao);

                if (resultado.Sucesso)
                {
                    etapa.EstadoEtapa = EstadoEtapa.Compensada;
                    _logger.LogInformation("Etapa {EtapaId} compensada com sucesso", etapa.Id);
                }
                else
                {
                    etapa.EstadoEtapa = EstadoEtapa.FalhaCompensacao;
                    _logger.LogError("Falha ao compensar etapa {EtapaId}: {Erro}",
                        etapa.Id, resultado.MensagemErro);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                etapa.EstadoEtapa = EstadoEtapa.FalhaCompensacao;
                
                var compensacao = new HistoricoCompensacao
                {
                    Id = Guid.NewGuid(),
                    EtapaSagaId = etapa.Id,
                    TipoCompensacao = etapa.TipoAcao.ToString(),
                    EstadoCompensacao = "Falhou",
                    DataInicio = DateTime.UtcNow,
                    DataFinalizacao = DateTime.UtcNow,
                    MensagemErro = ex.Message
                };

                _context.HistoricosCompensacao.Add(compensacao);
                await _context.SaveChangesAsync();

                _logger.LogError(ex, "Erro ao compensar etapa {EtapaId}", etapa.Id);
            }
        }

        saga.EstadoSaga = EstadoSaga.Compensada;
        saga.DataFinalizacao = DateTime.UtcNow;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogWarning("Saga {SagaId} compensada", sagaId);
    }

    public async Task<Saga?> ObterSagaAsync(Guid sagaId)
    {
        return await _context.Sagas
            .Include(s => s.Etapas)
            .ThenInclude(e => e.Compensacoes)
            .FirstOrDefaultAsync(s => s.Id == sagaId);
    }

    public async Task<List<Saga>> ListarSagasAsync(int pagina = 1, int tamanhoPagina = 50)
    {
        return await _context.Sagas
            .Include(s => s.Etapas)
            .OrderByDescending(s => s.DataCriacao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();
    }

    private IAcaoSaga ObterAcao(TipoAcao tipoAcao)
    {
        if (_mapaAcoes.TryGetValue(tipoAcao, out var tipoImplementacao))
        {
            var acao = _serviceProvider.GetService(tipoImplementacao) as IAcaoSaga;
            if (acao != null)
                return acao;
        }

        throw new InvalidOperationException($"Ação não encontrada para o tipo: {tipoAcao}");
    }

    private Dictionary<TipoAcao, Type> InicializarMapaAcoes()
    {
        // Mapear cada tipo de ação para sua implementação
        return new Dictionary<TipoAcao, Type>
        {
            { TipoAcao.ProcessarCotacao, typeof(AcaoProcessarCotacao) },
            { TipoAcao.CalcularMtmRendaFixa, typeof(AcaoCalcularMtmRendaFixa) },
            { TipoAcao.CalcularMtmRendaVariavel, typeof(AcaoCalcularMtmRendaVariavel) },
            { TipoAcao.ConsolidarMtm, typeof(AcaoConsolidarMtm) },
            { TipoAcao.CalcularValorizacaoContabil, typeof(AcaoCalcularValorizacaoContabil) },
            { TipoAcao.AtualizarPosicaoCliente, typeof(AcaoAtualizarPosicaoCliente) },
            { TipoAcao.ProcessarSplit, typeof(AcaoProcessarSplit) },
            { TipoAcao.ProcessarInsplit, typeof(AcaoProcessarInsplit) },
            { TipoAcao.AjustarPosicoes, typeof(AcaoAjustarPosicoes) },
            { TipoAcao.VerificarBarreira, typeof(AcaoVerificarBarreira) },
            { TipoAcao.ProcessarAtingimentoBarreira, typeof(AcaoProcessarAtingimentoBarreira) },
            { TipoAcao.IniciarLiquidacao, typeof(AcaoIniciarLiquidacao) },
            { TipoAcao.CalcularValorLiquidacao, typeof(AcaoCalcularValorLiquidacao) },
            { TipoAcao.LiquidarPosicoes, typeof(AcaoLiquidarPosicoes) },
            { TipoAcao.EncerrarCoe, typeof(AcaoEncerrarCoe) },
            { TipoAcao.LancarContabilidade, typeof(AcaoLancarContabilidade) },
            { TipoAcao.RealizarAjusteContabil, typeof(AcaoRealizarAjusteContabil) }
        };
    }
}

