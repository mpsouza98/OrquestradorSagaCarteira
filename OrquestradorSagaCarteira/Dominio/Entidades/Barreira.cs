using OrquestradorSagaCarteira.Dominio.Enums;

namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Barreira
{
    public Guid Id { get; set; }
    public Guid CoeId { get; set; }
    public string? TickerAtivo { get; set; } // Ativo específico da barreira (para observação individual)
    public TipoBarreira TipoBarreira { get; set; }
    public string Condicao { get; set; } = string.Empty; // UP ou DOWN
    public decimal NivelBarreira { get; set; }
    public DateTime DataObservacao { get; set; }
    public bool Atingida { get; set; }
    public DateTime? DataAtingimento { get; set; }
    public decimal? ValorAtingimento { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    
    public Coe Coe { get; set; } = null!;
}
