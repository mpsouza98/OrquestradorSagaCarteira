namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa uma liquidação agendada (desfazimento de operação)
/// </summary>
public class LiquidacaoAgendada
{
    public Guid Id { get; set; }
    public Guid OperacaoId { get; set; }
    public DateTime DataAgendamento { get; set; }
    public DateTime DataLiquidacao { get; set; }
    public decimal ValorLiquidacao { get; set; }
    public string Status { get; set; } = string.Empty; // Agendada, Processada, Cancelada
    public string Motivo { get; set; } = string.Empty;
    public DateTime? DataProcessamento { get; set; }
    public string? DadosLiquidacao { get; set; } // JSON
}

