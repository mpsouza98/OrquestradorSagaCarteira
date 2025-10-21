using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Dominio.Interfaces;

/// <summary>
/// Interface para ações executadas nas etapas da saga
/// </summary>
public interface IAcaoSaga
{
    /// <summary>
    /// Executa a ação da etapa
    /// </summary>
    Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa);
    
    /// <summary>
    /// Compensa a ação executada (rollback)
    /// </summary>
    Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa);
}

public class ResultadoAcao
{
    public bool Sucesso { get; set; }
    public string? DadosSaida { get; set; }
    public string? MensagemErro { get; set; }
}

public class ResultadoCompensacao
{
    public bool Sucesso { get; set; }
    public string? DadosCompensacao { get; set; }
    public string? MensagemErro { get; set; }
}

