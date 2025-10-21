using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

/// <summary>
/// Interface para o orquestrador de sagas
/// </summary>
public interface IOrquestradorSaga
{
    /// <summary>
    /// Inicia uma nova saga
    /// </summary>
    Task<Saga> IniciarSagaAsync(Saga saga);
    
    /// <summary>
    /// Executa a próxima etapa da saga
    /// </summary>
    Task ExecutarProximaEtapaAsync(Guid sagaId);
    
    /// <summary>
    /// Compensa as etapas executadas em caso de falha
    /// </summary>
    Task CompensarSagaAsync(Guid sagaId);
    
    /// <summary>
    /// Obtém o estado atual da saga
    /// </summary>
    Task<Saga?> ObterSagaAsync(Guid sagaId);
    
    /// <summary>
    /// Lista todas as sagas
    /// </summary>
    Task<List<Saga>> ListarSagasAsync(int pagina = 1, int tamanhoPagina = 50);
}

