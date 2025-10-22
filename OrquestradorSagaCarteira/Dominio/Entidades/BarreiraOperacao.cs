namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa uma barreira configurada para uma operação
/// </summary>
public class BarreiraOperacao
{
    public Guid Id { get; set; }
    public Guid OperacaoId { get; set; }
    public string? Ticker { get; set; } // null = barreira de cesta
    public string TipoBarreira { get; set; } = string.Empty; // Autocall, KnockIn, KnockOut
    public string Condicao { get; set; } = string.Empty; // UP ou DOWN
    public decimal NivelBarreira { get; set; }
    public DateTime DataObservacao { get; set; }
    public bool Atingida { get; set; }
    public DateTime? DataAtingimento { get; set; }
    public decimal? ValorAtingimento { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    
    public Operacao Operacao { get; set; } = null!;
}

