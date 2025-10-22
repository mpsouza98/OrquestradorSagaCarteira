namespace Calculadora.Core.Models;

/// <summary>
/// Resultado do cálculo de autocall
/// </summary>
public class ResultadoAutocall
{
    public bool AutocallAtingido { get; set; }
    public decimal ValorCesta { get; set; }
    public decimal NivelBarreira { get; set; }
    public string TipoEstrutura { get; set; } = string.Empty; // BestOf, WorstOf
    public Dictionary<string, decimal> PerformanceAtivos { get; set; } = new();
}

