using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Enums;
using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Servicos;

public class OrquestradorSaga : IOrquestradorSaga
{
    private readonly ISagaRepository _sagaRepository;
    private readonly ILogger<OrquestradorSaga> _logger;
    private readonly Dictionary<TipoAcao, IAcaoSaga> _mapaAcoes;

    public OrquestradorSaga(
        ISagaRepository sagaRepository,
        ILogger<OrquestradorSaga> logger,
        IEnumerable<IAcaoSaga> acoes)
    {
        _sagaRepository = sagaRepository;
        _logger = logger;
        _mapaAcoes = InicializarMapaAcoes(acoes);
    }

    public async Task<Saga> IniciarSagaAsync(Saga saga)
    {
        saga.Id = Guid.NewGuid();
        saga.EstadoSaga = EstadoSaga.Iniciada;
        saga.DataCriacao = DateTime.UtcNow;
        saga.DataAtualizacao = DateTime.UtcNow;
        
        // Atribuir IDs e ordem às etapas
        for (var i = 0; i < saga.Etapas.Count; i++)
        {
            saga.Etapas[i].Id = Guid.NewGuid();
            saga.Etapas[i].SagaId = saga.Id;
            saga.Etapas[i].OrdemExecucao = i + 1;
            saga.Etapas[i].EstadoEtapa = EstadoEtapa.Pendente;
        }

        await _sagaRepository.InserirAsync(saga);

        _logger.LogInformation("📋 Saga {SagaId} iniciada - Tipo: {TipoSaga}, Etapas: {NumEtapas}",
            saga.Id, saga.TipoSaga, saga.Etapas.Count);

        // Iniciar execução
        await ExecutarProximaEtapaAsync(saga.Id);

        return saga;
    }

    public async Task ExecutarProximaEtapaAsync(Guid sagaId)
    {
        var saga = await _sagaRepository.ObterPorIdAsync(sagaId);

        if (saga == null)
        {
            _logger.LogError("❌ Saga {SagaId} não encontrada", sagaId);
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
            await _sagaRepository.AtualizarAsync(saga);

            _logger.LogInformation("✅ Saga {SagaId} concluída com sucesso", sagaId);
            return;
        }

        // Executar etapa
        proximaEtapa.EstadoEtapa = EstadoEtapa.EmExecucao;
        proximaEtapa.DataInicio = DateTime.UtcNow;
        saga.EstadoSaga = EstadoSaga.EmExecucao;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _sagaRepository.AtualizarAsync(saga);

        _logger.LogInformation("▶️ Executando etapa {Ordem}/{Total} - {NomeEtapa}",
            proximaEtapa.OrdemExecucao, saga.Etapas.Count, proximaEtapa.NomeEtapa);

        try
        {
            // Transferir dados da etapa anterior se necessário
            if (proximaEtapa.OrdemExecucao > 1)
            {
                var etapaAnterior = saga.Etapas
                    .FirstOrDefault(e => e.OrdemExecucao == proximaEtapa.OrdemExecucao - 1);
                
                if (etapaAnterior?.DadosSaida != null && (proximaEtapa.DadosEntrada == "{}" || string.IsNullOrEmpty(proximaEtapa.DadosEntrada)))
                {
                    // Transformar dados entre etapas conforme necessário
                    proximaEtapa.DadosEntrada = TransformarDadosEntreEtapas(
                        etapaAnterior.TipoAcao, 
                        proximaEtapa.TipoAcao, 
                        etapaAnterior.DadosSaida);
                }
            }

            var acao = ObterAcao(proximaEtapa.TipoAcao);
            var resultado = await acao.ExecutarAsync(proximaEtapa);

            if (resultado.Sucesso)
            {
                proximaEtapa.EstadoEtapa = EstadoEtapa.Concluida;
                proximaEtapa.DataFinalizacao = DateTime.UtcNow;
                proximaEtapa.DadosSaida = resultado.DadosSaida;
                await _sagaRepository.AtualizarAsync(saga);

                _logger.LogInformation("✔️ Etapa {NomeEtapa} concluída", proximaEtapa.NomeEtapa);

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
                await _sagaRepository.AtualizarAsync(saga);

                _logger.LogError("❌ Etapa {NomeEtapa} falhou: {Erro}",
                    proximaEtapa.NomeEtapa, resultado.MensagemErro);

                await CompensarSagaAsync(sagaId);
            }
        }
        catch (Exception ex)
        {
            proximaEtapa.EstadoEtapa = EstadoEtapa.Falhou;
            proximaEtapa.DataFinalizacao = DateTime.UtcNow;
            proximaEtapa.MensagemErro = ex.Message;
            proximaEtapa.Tentativas++;
            await _sagaRepository.AtualizarAsync(saga);

            _logger.LogError(ex, "❌ Erro ao executar etapa {NomeEtapa}", proximaEtapa.NomeEtapa);

            await CompensarSagaAsync(sagaId);
        }
    }

    public async Task CompensarSagaAsync(Guid sagaId)
    {
        var saga = await _sagaRepository.ObterPorIdAsync(sagaId);

        if (saga == null)
        {
            _logger.LogError("❌ Saga {SagaId} não encontrada para compensação", sagaId);
            return;
        }

        saga.EstadoSaga = EstadoSaga.Compensando;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _sagaRepository.AtualizarAsync(saga);

        _logger.LogWarning("⚠️ Iniciando compensação da Saga {SagaId}", sagaId);

        // Compensar etapas na ordem inversa
        var etapasParaCompensar = saga.Etapas
            .Where(e => e.EstadoEtapa == EstadoEtapa.Concluida)
            .OrderByDescending(e => e.OrdemExecucao)
            .ToList();

        foreach (var etapa in etapasParaCompensar)
        {
            etapa.EstadoEtapa = EstadoEtapa.Compensando;
            await _sagaRepository.AtualizarAsync(saga);

            try
            {
                var acao = ObterAcao(etapa.TipoAcao);
                var resultado = await acao.CompensarAsync(etapa);

                if (resultado.Sucesso)
                {
                    etapa.EstadoEtapa = EstadoEtapa.Compensada;
                    _logger.LogInformation("↩️ Etapa {NomeEtapa} compensada", etapa.NomeEtapa);
                }
                else
                {
                    etapa.EstadoEtapa = EstadoEtapa.FalhaCompensacao;
                    _logger.LogError("❌ Falha ao compensar etapa {NomeEtapa}: {Erro}",
                        etapa.NomeEtapa, resultado.MensagemErro);
                }

                await _sagaRepository.AtualizarAsync(saga);
            }
            catch (Exception ex)
            {
                etapa.EstadoEtapa = EstadoEtapa.FalhaCompensacao;
                await _sagaRepository.AtualizarAsync(saga);

                _logger.LogError(ex, "❌ Erro ao compensar etapa {NomeEtapa}", etapa.NomeEtapa);
            }
        }

        saga.EstadoSaga = EstadoSaga.Compensada;
        saga.DataFinalizacao = DateTime.UtcNow;
        saga.DataAtualizacao = DateTime.UtcNow;
        await _sagaRepository.AtualizarAsync(saga);

        _logger.LogWarning("⚠️ Saga {SagaId} compensada", sagaId);
    }

    public async Task<Saga?> ObterSagaAsync(Guid sagaId)
    {
        return await _sagaRepository.ObterPorIdAsync(sagaId);
    }

    public async Task<List<Saga>> ListarSagasAsync(int pagina = 1, int tamanhoPagina = 50)
    {
        return await _sagaRepository.ListarAsync(pagina, tamanhoPagina);
    }

    private IAcaoSaga ObterAcao(TipoAcao tipoAcao)
    {
        if (_mapaAcoes.TryGetValue(tipoAcao, out var acao))
        {
            return acao;
        }

        throw new InvalidOperationException($"Ação não encontrada para o tipo: {tipoAcao}");
    }

    private static Dictionary<TipoAcao, IAcaoSaga> InicializarMapaAcoes(IEnumerable<IAcaoSaga> acoes)
    {
        var mapa = new Dictionary<TipoAcao, IAcaoSaga>();
        
        foreach (var acao in acoes)
        {
            var tipoAcao = acao.GetType().Name switch
            {
                nameof(Acoes.AcaoVerificarBarreira) => TipoAcao.VerificarBarreira,
                nameof(Acoes.AcaoPersistirBarreira) => TipoAcao.PersistirBarreira,
                nameof(Acoes.AcaoNotificarBarreiraAtingida) => TipoAcao.NotificarBarreiraAtingida,
                nameof(Acoes.AcaoAgregarCesta) => TipoAcao.AgregarCesta,
                nameof(Acoes.AcaoVerificarAutoCall) => TipoAcao.VerificarAutoCall,
                nameof(Acoes.AcaoPersistirAutoCall) => TipoAcao.PersistirAutoCall,
                nameof(Acoes.AcaoNotificarAutoCall) => TipoAcao.NotificarAutoCall,
                nameof(Acoes.AcaoAgendarDesfazimentoOperacao) => TipoAcao.AgendarDesfazimentoOperacao,
                _ => throw new InvalidOperationException($"Ação não mapeada: {acao.GetType().Name}")
            };
            
            mapa[tipoAcao] = acao;
        }

        return mapa;
    }

    private string TransformarDadosEntreEtapas(TipoAcao tipoAcaoOrigem, TipoAcao tipoAcaoDestino, string dadosEntrada)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(dadosEntrada);
            var root = doc.RootElement;

            switch (tipoAcaoOrigem)
            {
                // Transformação: VerificarBarreira -> PersistirBarreira
                case TipoAcao.VerificarBarreira when tipoAcaoDestino == TipoAcao.PersistirBarreira:
                {
                    var resultados = root.GetProperty("Resultados");
                    var fonte = root.TryGetProperty("Ticker", out var tickerProp) ? tickerProp.GetString() : "";
                
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        BarreirasAtingidas = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(resultados.GetRawText()),
                        Fonte = fonte
                    });
                }
                // Transformação: PersistirBarreira -> NotificarBarreiraAtingida
                case TipoAcao.PersistirBarreira when tipoAcaoDestino == TipoAcao.NotificarBarreiraAtingida:
                {
                    var eventos = root.GetProperty("Eventos");
                
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Eventos = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(eventos.GetRawText())
                    });
                }
                case TipoAcao.NotificarBarreiraAtingida:
                case TipoAcao.AgregarCesta:
                case TipoAcao.VerificarAutoCall:
                case TipoAcao.PersistirAutoCall:
                case TipoAcao.NotificarAutoCall:
                case TipoAcao.AgendarDesfazimentoOperacao:
                default:
                    // Por padrão, retorna os dados sem transformação
                    return dadosEntrada;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao transformar dados entre etapas {Origem} -> {Destino}. Usando dados originais.", 
                tipoAcaoOrigem, tipoAcaoDestino);
            return dadosEntrada;
        }
    }
}
