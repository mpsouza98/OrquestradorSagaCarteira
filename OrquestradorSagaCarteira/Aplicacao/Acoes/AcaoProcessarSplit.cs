using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoProcessarSplit : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoProcessarSplit> _logger;

    public AcaoProcessarSplit(CarteiraDbContext context, ILogger<AcaoProcessarSplit> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosEventoCorporativo>(etapa.DadosEntrada ?? "{}");
            
            var evento = await _context.EventosCorporativos.FindAsync(dados!.EventoId);
            if (evento == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Evento não encontrado" };

            var ativos = await _context.AtivosCoe
                .Where(a => a.TickerAtivo == evento.CodigoAtivo)
                .ToListAsync();

            var ajustesRealizados = new List<AjusteAtivo>();

            foreach (var ativo in ativos)
            {
                var quantidadeAnterior = ativo.Quantidade ?? 0;
                var precoAnterior = ativo.PrecoInicial ?? 0;
                
                ativo.Quantidade = quantidadeAnterior * (evento.FatorAjuste ?? 1);
                ativo.PrecoInicial = precoAnterior / (evento.FatorAjuste ?? 1);
                
                ajustesRealizados.Add(new AjusteAtivo
                {
                    AtivoId = ativo.Id,
                    QuantidadeAnterior = quantidadeAnterior,
                    PrecoAnterior = precoAnterior
                });
                
                _logger.LogInformation("Split aplicado ao ativo {AtivoId}: Qtd {QtdAnt} -> {QtdNova}", 
                    ativo.Id, quantidadeAnterior, ativo.Quantidade);
            }

            evento.Processado = true;
            evento.DataProcessamento = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { Ajustes = ajustesRealizados })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar split");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<DadosSaidaSplit>(etapa.DadosSaida ?? "{}");
            
            if (dadosSaida?.Ajustes != null)
            {
                foreach (var ajuste in dadosSaida.Ajustes)
                {
                    var ativo = await _context.AtivosCoe.FindAsync(ajuste.AtivoId);
                    if (ativo != null)
                    {
                        ativo.Quantidade = ajuste.QuantidadeAnterior;
                        ativo.PrecoInicial = ajuste.PrecoAnterior;
                    }
                }

                var dados = JsonSerializer.Deserialize<DadosEventoCorporativo>(etapa.DadosEntrada ?? "{}");
                var evento = await _context.EventosCorporativos.FindAsync(dados!.EventoId);
                if (evento != null)
                {
                    evento.Processado = false;
                    evento.DataProcessamento = null;
                }

                await _context.SaveChangesAsync();
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar split");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosEventoCorporativo
    {
        public Guid EventoId { get; set; }
    }

    private class DadosSaidaSplit
    {
        public List<AjusteAtivo>? Ajustes { get; set; }
    }

    private class AjusteAtivo
    {
        public Guid AtivoId { get; set; }
        public decimal QuantidadeAnterior { get; set; }
        public decimal PrecoAnterior { get; set; }
    }
}
