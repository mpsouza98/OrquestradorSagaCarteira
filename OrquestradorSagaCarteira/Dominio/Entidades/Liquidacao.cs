namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Liquidacao
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public Guid? BarreiraId { get; set; }
    public string TipoLiquidacao { get; set; } = string.Empty; // AUTOCALL, VENCIMENTO, ANTECIPADO
    public DateTime DataLiquidacao { get; set; }
    public decimal ValorLiquidacao { get; set; }
    public decimal? PercentualRetorno { get; set; }
    public string? Motivo { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataProcessamento { get; set; }
    
    public Coe Coe { get; set; } = null!;
    public Barreira? Barreira { get; set; }
}

