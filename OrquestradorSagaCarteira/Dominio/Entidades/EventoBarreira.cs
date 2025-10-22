namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa um evento de barreira atingida
/// </summary>
public class EventoBarreira
{
    public Guid Id { get; set; }
    public Guid BarreiraId { get; set; }
    public Guid OperacaoId { get; set; }
    public string? Ticker { get; set; }
    public decimal ValorObservado { get; set; }
    public decimal NivelBarreira { get; set; }
    public string TipoBarreira { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public string DadosEvento { get; set; } = string.Empty; // JSON
}

