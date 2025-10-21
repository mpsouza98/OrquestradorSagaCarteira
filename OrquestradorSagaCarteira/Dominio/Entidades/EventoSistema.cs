namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class EventoSistema
{
    public Guid Id { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public string Topico { get; set; } = string.Empty;
    public string DadosEvento { get; set; } = string.Empty; // JSON
    public DateTime DataPublicacao { get; set; }
    public bool Processado { get; set; }
    public DateTime? DataProcessamento { get; set; }
}

