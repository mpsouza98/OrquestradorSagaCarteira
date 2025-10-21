namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class ValorizacaoContabil
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public DateTime DataReferencia { get; set; }
    public decimal ValorContabil { get; set; }
    public decimal ValorMercado { get; set; }
    public decimal Diferenca { get; set; }
    public decimal? AjusteContabil { get; set; }
    public string? ContaDebito { get; set; }
    public string? ContaCredito { get; set; }
    public DateTime DataCriacao { get; set; }
    
    public Coe Coe { get; set; } = null!;
}

