namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Coe
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public decimal ValorNominal { get; set; }
    public decimal PercentualRendaFixa { get; set; }
    public decimal PercentualRendaVariavel { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    
    public List<AtivoCoe> Ativos { get; set; } = new();
    public List<Barreira> Barreiras { get; set; } = new();
    public List<Mtm> Mtms { get; set; } = new();
    public List<PosicaoCliente> Posicoes { get; set; } = new();
    public List<ValorizacaoContabil> Valorizacoes { get; set; } = new();
    public List<Liquidacao> Liquidacoes { get; set; } = new();
}

