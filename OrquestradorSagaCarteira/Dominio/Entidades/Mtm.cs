namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Mtm
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public DateTime DataReferencia { get; set; }
    public decimal? ValorRendaFixa { get; set; }
    public decimal? ValorRendaVariavel { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal? PercentualRentabilidade { get; set; }
    public DateTime DataCriacao { get; set; }
    
    public Coe Coe { get; set; } = null!;
}

