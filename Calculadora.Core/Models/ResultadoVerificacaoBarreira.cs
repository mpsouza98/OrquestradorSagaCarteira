namespace Calculadora.Core.Models;

/// <summary>
/// Resultado da verificação de barreira
/// </summary>
public class ResultadoVerificacaoBarreira
{
    public bool BarreiraAtingida { get; set; }
    public decimal ValorObservado { get; set; }
    public decimal NivelBarreira { get; set; }
    public decimal TaxaVariacao { get; set; }
    public string Condicao { get; set; } = string.Empty;
}

