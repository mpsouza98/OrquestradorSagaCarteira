namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class PosicaoCliente
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public string CodigoCliente { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorInvestido { get; set; }
    public decimal? ValorAtual { get; set; }
    public DateTime DataAquisicao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public bool Ativo { get; set; }
    
    public Coe Coe { get; set; } = null!;
}

