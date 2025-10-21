namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Mtm
{
    public Guid Id { get; set; }
    public int CodigoOperacao { get; set; }
    public decimal ValorMtm { get; set; }
    public decimal ValorAccrual { get; set; }
    public int SequencialPerna { get; set; }
    public DateTime DataReferencia { get; set; }
    public DateTime DataCriacao { get; set; }
}
