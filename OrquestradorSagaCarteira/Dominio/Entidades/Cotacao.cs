namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Cotacao
{
    public Guid Id { get; set; }
    public string CodigoAtivo { get; set; } = string.Empty;
    public string TipoAtivo { get; set; } = string.Empty;
    public DateTime DataReferencia { get; set; }
    public decimal? PrecoAbertura { get; set; }
    public decimal? PrecoFechamento { get; set; }
    public decimal? PrecoMaximo { get; set; }
    public decimal? PrecoMinimo { get; set; }
    public decimal? Volume { get; set; }
    public DateTime DataCriacao { get; set; }
}

