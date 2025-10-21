using OrquestradorSagaCarteira.Dominio.Enums;

namespace OrquestradorSagaCarteira.Dominio.Entidades;

public class EtapaSaga
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public string NomeEtapa { get; set; } = string.Empty;
    public int OrdemExecucao { get; set; }
    public EstadoEtapa EstadoEtapa { get; set; }
    public TipoAcao TipoAcao { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFinalizacao { get; set; }
    public string? DadosEntrada { get; set; } // JSON
    public string? DadosSaida { get; set; } // JSON
    public string? MensagemErro { get; set; }
    public int Tentativas { get; set; }
    
    public Saga Saga { get; set; } = null!;
    public List<HistoricoCompensacao> Compensacoes { get; set; } = new();
}

