using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoLiquidarPosicoes : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoLiquidarPosicoes> _logger;

    public AcaoLiquidarPosicoes(CarteiraDbContext context, ILogger<AcaoLiquidarPosicoes> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosLiquidacao>(etapa.DadosEntrada ?? "{}");
            
            var liquidacao = await _context.Liquidacoes.FindAsync(dados!.LiquidacaoId);
            if (liquidacao == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "Liquidação não encontrada" };

            var posicoes = await _context.PosicoesCliente
                .Where(p => p.CoeId == liquidacao.CoeId && p.Ativo)
                .ToListAsync();

            var posicoesLiquidadas = new List<Guid>();

            foreach (var posicao in posicoes)
            {
                posicao.ValorAtual = (posicao.Quantidade / 1000) * liquidacao.ValorLiquidacao;
                posicao.Ativo = false;
                posicao.DataAtualizacao = DateTime.UtcNow;
                
                posicoesLiquidadas.Add(posicao.Id);
                
                _logger.LogInformation("Posição {PosicaoId} liquidada com valor {Valor}", 
                    posicao.Id, posicao.ValorAtual);
            }

            liquidacao.Status = "POSICOES_LIQUIDADAS";
            await _context.SaveChangesAsync();

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new { PosicoesLiquidadas = posicoesLiquidadas })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liquidar posições");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            var dadosSaida = JsonSerializer.Deserialize<DadosSaidaLiquidacao>(etapa.DadosSaida ?? "{}");
            
            if (dadosSaida?.PosicoesLiquidadas != null)
            {
                foreach (var posicaoId in dadosSaida.PosicoesLiquidadas)
                {
                    var posicao = await _context.PosicoesCliente.FindAsync(posicaoId);
                    if (posicao != null)
                    {
                        posicao.Ativo = true;
                        posicao.DataAtualizacao = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Posições reativadas na compensação");
            }

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar liquidação de posições");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosLiquidacao
    {
        public Guid LiquidacaoId { get; set; }
    }

    private class DadosSaidaLiquidacao
    {
        public List<Guid>? PosicoesLiquidadas { get; set; }
    }
}

