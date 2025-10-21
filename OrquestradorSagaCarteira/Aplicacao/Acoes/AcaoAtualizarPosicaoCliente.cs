using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrquestradorSagaCarteira.Aplicacao.Acoes;

public class AcaoAtualizarPosicaoCliente : IAcaoSaga
{
    private readonly CarteiraDbContext _context;
    private readonly ILogger<AcaoAtualizarPosicaoCliente> _logger;

    public AcaoAtualizarPosicaoCliente(CarteiraDbContext context, ILogger<AcaoAtualizarPosicaoCliente> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoAcao> ExecutarAsync(EtapaSaga etapa)
    {
        try
        {
            var dados = JsonSerializer.Deserialize<DadosPosicao>(etapa.DadosEntrada ?? "{}");
            
            var mtm = await _context.Mtms
                .Where(m => m.CoeId == dados!.CoeId && m.DataReferencia == dados.DataReferencia)
                .FirstOrDefaultAsync();

            if (mtm == null)
                return new ResultadoAcao { Sucesso = false, MensagemErro = "MTM não encontrado" };

            var posicoes = await _context.PosicoesCliente
                .Where(p => p.CoeId == dados.CoeId && p.Ativo)
                .ToListAsync();

            var posicoesAtualizadas = new List<Guid>();

            foreach (var posicao in posicoes)
            {
                var valorAnterior = posicao.ValorAtual;
                posicao.ValorAtual = (posicao.Quantidade / 1000) * mtm.ValorTotal; // Simplificado
                posicao.DataAtualizacao = DateTime.UtcNow;
                
                posicoesAtualizadas.Add(posicao.Id);
                
                _logger.LogInformation("Posição {PosicaoId} atualizada de {ValorAnterior} para {ValorAtual}", 
                    posicao.Id, valorAnterior, posicao.ValorAtual);
            }

            await _context.SaveChangesAsync();

            return new ResultadoAcao
            {
                Sucesso = true,
                DadosSaida = JsonSerializer.Serialize(new 
                { 
                    PosicoesAtualizadas = posicoesAtualizadas,
                    ValorMtm = mtm.ValorTotal
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar posição cliente");
            return new ResultadoAcao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    public async Task<ResultadoCompensacao> CompensarAsync(EtapaSaga etapa)
    {
        try
        {
            // Buscar MTM anterior para reverter valores
            var dados = JsonSerializer.Deserialize<DadosPosicao>(etapa.DadosEntrada ?? "{}");
            
            var mtmAnterior = await _context.Mtms
                .Where(m => m.CoeId == dados!.CoeId && m.DataReferencia < dados.DataReferencia)
                .OrderByDescending(m => m.DataReferencia)
                .FirstOrDefaultAsync();

            var posicoes = await _context.PosicoesCliente
                .Where(p => p.CoeId == dados.CoeId && p.Ativo)
                .ToListAsync();

            foreach (var posicao in posicoes)
            {
                if (mtmAnterior != null)
                {
                    posicao.ValorAtual = (posicao.Quantidade / 1000) * mtmAnterior.ValorTotal;
                }
                posicao.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Posições revertidas para MTM anterior");

            return new ResultadoCompensacao { Sucesso = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao compensar atualização de posição");
            return new ResultadoCompensacao { Sucesso = false, MensagemErro = ex.Message };
        }
    }

    private class DadosPosicao
    {
        public Guid CoeId { get; set; }
        public DateTime DataReferencia { get; set; }
    }
}

