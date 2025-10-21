using OrquestradorSagaCarteira.Dominio.Enums;

namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class Saga
{
    public Guid Id { get; set; }
    public TipoSaga TipoSaga { get; set; }
    public EstadoSaga EstadoSaga { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public DateTime? DataFinalizacao { get; set; }
    public string? DadosContexto { get; set; } // JSON
    public string? MensagemErro { get; set; }
    
    public List<EtapaSaga> Etapas { get; set; } = new();
}

