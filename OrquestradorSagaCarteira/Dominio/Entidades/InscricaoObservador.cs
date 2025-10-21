namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class InscricaoObservador
{
    public Guid Id { get; set; }
    public string Topico { get; set; } = string.Empty;
    public string NomeObservador { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
}

