namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class HistoricoCompensacao
{
    public Guid Id { get; set; }
    public Guid EtapaSagaId { get; set; }
    public string TipoCompensacao { get; set; } = string.Empty;
    public string EstadoCompensacao { get; set; } = string.Empty;
    public string? DadosCompensacao { get; set; } // JSON
    public DateTime DataInicio { get; set; }
    public DateTime? DataFinalizacao { get; set; }
    public string? MensagemErro { get; set; }
    
    public EtapaSaga EtapaSaga { get; set; } = null!;
}

