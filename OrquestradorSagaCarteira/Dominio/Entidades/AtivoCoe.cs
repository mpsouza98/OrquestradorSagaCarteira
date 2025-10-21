namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class AtivoCoe
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public string TickerAtivo { get; set; } = string.Empty;
    public string TipoAtivo { get; set; } = string.Empty;
    public decimal PercentualParticipacao { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? PrecoInicial { get; set; }
    public decimal CotacaoInicial { get; set; } // Cotação do ativo no início do COE
    public DateTime DataCriacao { get; set; }
    
    public Coe Coe { get; set; } = null!;
}
