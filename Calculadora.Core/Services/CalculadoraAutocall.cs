using Calculadora.Core.Models;

namespace Calculadora.Core.Services;

/// <summary>
/// Serviço para cálculos de autocall
/// </summary>
public class CalculadoraAutocall
{
    /// <summary>
    /// Verifica autocall com base na estrutura da cesta (BestOf ou WorstOf)
    /// </summary>
    public ResultadoAutocall VerificarAutocall(
        List<AtivoCesta> ativos,
        decimal nivelBarreira,
        string tipoEstrutura)
    {
        var performances = new Dictionary<string, decimal>();
        
        // Calcular performance de cada ativo
        foreach (var ativo in ativos)
        {
            var performance = CalcularPerformance(ativo.CotacaoInicial, ativo.CotacaoAtual);
            performances[ativo.Ticker] = performance;
        }

        // Determinar valor da cesta baseado no tipo de estrutura
        decimal valorCesta = tipoEstrutura.ToUpper() switch
        {
            "BESTOF" => performances.Values.Max(),
            "WORSTOF" => performances.Values.Min(),
            _ => 0
        };

        var autocallAtingido = valorCesta >= nivelBarreira;

        return new ResultadoAutocall
        {
            AutocallAtingido = autocallAtingido,
            ValorCesta = valorCesta,
            NivelBarreira = nivelBarreira,
            TipoEstrutura = tipoEstrutura,
            PerformanceAtivos = performances
        };
    }

    /// <summary>
    /// Calcula a performance (retorno percentual) de um ativo
    /// </summary>
    public decimal CalcularPerformance(decimal cotacaoInicial, decimal cotacaoAtual)
    {
        if (cotacaoInicial == 0)
            return 0;

        return ((cotacaoAtual - cotacaoInicial) / cotacaoInicial) * 100;
    }

    /// <summary>
    /// Agrega valores da cesta considerando participação de cada ativo
    /// </summary>
    public decimal AgregarCesta(List<AtivoCesta> ativos, string tipoEstrutura)
    {
        var performances = ativos
            .Select(a => new
            {
                a.Ticker,
                Performance = CalcularPerformance(a.CotacaoInicial, a.CotacaoAtual),
                a.PercentualParticipacao
            })
            .ToList();

        return tipoEstrutura.ToUpper() switch
        {
            "BESTOF" => performances.Max(p => p.Performance),
            "WORSTOF" => performances.Min(p => p.Performance),
            _ => performances.Sum(p => p.Performance * (p.PercentualParticipacao / 100))
        };
    }
}

