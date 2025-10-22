namespace Calculadora.Core.Models;

/// <summary>
/// Representa um ativo na cesta para cálculo
/// </summary>
public class AtivoCesta
{
    public string Ticker { get; set; } = string.Empty;
    public decimal CotacaoInicial { get; set; }
    public decimal CotacaoAtual { get; set; }
    public decimal PercentualParticipacao { get; set; }
}

