using OrquestradorSagaCarteira.Dominio.Enums;

namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class EventoCorporativo
{
    public Guid Id { get; set; }
    public string CodigoAtivo { get; set; } = string.Empty;
    public TipoEventoCorporativo TipoEvento { get; set; }
    public DateTime DataEvento { get; set; }
    public DateTime? DataCom { get; set; }
    public decimal? FatorAjuste { get; set; }
    public string? Descricao { get; set; }
    public bool Processado { get; set; }
    public DateTime? DataProcessamento { get; set; }
    public DateTime DataCriacao { get; set; }
}

