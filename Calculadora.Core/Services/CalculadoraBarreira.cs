using Calculadora.Core.Models;

namespace Calculadora.Core.Services;

/// <summary>
/// Serviço para cálculos relacionados a barreiras
/// </summary>
public class CalculadoraBarreira
{
    /// <summary>
    /// Verifica se uma barreira foi atingida
    /// </summary>
    public ResultadoVerificacaoBarreira VerificarBarreira(
        decimal cotacaoInicial,
        decimal cotacaoAtual,
        decimal nivelBarreira,
        string condicao)
    {
        var taxaVariacao = CalcularTaxaVariacao(cotacaoInicial, cotacaoAtual);
        
        var barreiraAtingida = condicao.ToUpper() switch
        {
            "UP" => taxaVariacao >= nivelBarreira,
            "DOWN" => taxaVariacao <= nivelBarreira,
            _ => false
        };

        return new ResultadoVerificacaoBarreira
        {
            BarreiraAtingida = barreiraAtingida,
            ValorObservado = cotacaoAtual,
            NivelBarreira = nivelBarreira,
            TaxaVariacao = taxaVariacao,
            Condicao = condicao
        };
    }

    /// <summary>
    /// Calcula a taxa de variação percentual
    /// </summary>
    public decimal CalcularTaxaVariacao(decimal valorInicial, decimal valorAtual)
    {
        if (valorInicial == 0)
            return 0;

        return ((valorAtual - valorInicial) / valorInicial) * 100;
    }
}

