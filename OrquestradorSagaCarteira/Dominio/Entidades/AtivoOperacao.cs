namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa um ativo dentro de uma operação
/// </summary>
public class AtivoOperacao
{
    public Guid Id { get; set; }
    public Guid OperacaoId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoInicial { get; set; }
    public decimal PercentualParticipacao { get; set; }
    public DateTime DataCriacao { get; set; }
    
    public Operacao Operacao { get; set; } = null!;
}

