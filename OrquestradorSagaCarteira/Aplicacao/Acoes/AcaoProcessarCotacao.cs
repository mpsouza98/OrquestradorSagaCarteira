using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoProcessarCotacao : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoProcessarCotacao> _logger;

    public AcaoProcessarCotacao(CarteiraDbContext context, ILogger<AcaoProcessarCotacao> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosCotacao>(etapa.DadosEntrada ?? "{}");
            
            var cotacao = new Cotacao
            {
                Id = Guid.NewGuid(),
                CodigoAtivo = dados!.CodigoAtivo,
                TipoAtivo = dados.TipoAtivo,
                DataReferencia = dados.DataReferencia,
                PrecoAbertura = dados.PrecoAbertura,
                PrecoFechamento = dados.PrecoFechamento,
                PrecoMaximo = dados.PrecoMaximo,
                PrecoMinimo = dados.PrecoMinimo,
                Volume = dados.Volume,
                DataCriacao = DateTime.UtcNow
            };

            _context.Cotacoes.Add(cotacao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cotação processada: {CodigoAtivo} em {Data}", 
                cotacao.CodigoAtivo, cotacao.DataReferencia);

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { CotacaoId = cotacao.Id })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar cotação");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<Dictionary<string, string>>(etapa.DadosSaida ?? "{}");
            if (dadosSaida != null && dadosSaida.TryGetValue("CotacaoId", out var cotacaoIdStr))
            {
                var cotacaoId = Guid.Parse(cotacaoIdStr);
                var cotacao = await _context.Cotacoes.FindAsync(cotacaoId);
                
                if (cotacao != null)
                {
                    _context.Cotacoes.Remove(cotacao);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Cotação {CotacaoId} removida na compensação", cotacaoId);
                }
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar processamento de cotação");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosCotacao
    {
        public string CodigoAtivo { get; set; } = string.Empty;
        public string TipoAtivo { get; set; } = string.Empty;
        public DateTime DataReferencia { get; set; }
        public decimal? PrecoAbertura { get; set; }
        public decimal? PrecoFechamento { get; set; }
        public decimal? PrecoMaximo { get; set; }
        public decimal? PrecoMinimo { get; set; }
        public decimal? Volume { get; set; }
    }
}

