
namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa uma operação (instância) dentro da saga
/// </summary>
public class Operacao
{
    public Guid Id { get; set; }
    public string CodigoOperacao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataVencimento { get; set; }
    public decimal ValorNominal { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty; // BestOf, WorstOf
    public bool Ativa { get; set; }
    public DateTime DataAtualizacao { get; set; }
    
    public List<AtivoOperacao> Ativos { get; set; } = new();
    public List<BarreiraOperacao> Barreiras { get; set; } = new();
}

