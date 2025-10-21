namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Cotacao
{
    public Guid Id { get; set; }
    public int CodigoCotacao { get; set; }
    public string TickerAtivo { get; set; } = string.Empty;
    public string Fonte { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public decimal PrecoFechamento { get; set; }
    public DateTime DataCriacao { get; set; }
}
