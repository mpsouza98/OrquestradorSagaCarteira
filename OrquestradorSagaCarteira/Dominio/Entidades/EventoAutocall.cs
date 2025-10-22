namespace OrquestradorSagaCarteira.Dominio.Entidades;

/// <summary>
/// Representa um evento de autocall verificado
/// </summary>
public class EventoAutocall
{
    public Guid Id { get; set; }
    public Guid OperacaoId { get; set; }
    public bool AutocallAtingido { get; set; }
    public decimal? ValorCesta { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty; // BestOf, WorstOf
    public DateTime DataEvento { get; set; }
    public string DadosEvento { get; set; } = string.Empty; // JSON com detalhes dos ativos
}

