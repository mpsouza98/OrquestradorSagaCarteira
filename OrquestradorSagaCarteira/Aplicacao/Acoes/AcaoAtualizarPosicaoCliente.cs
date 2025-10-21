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
            
            // MTM agora é pré-calculado na base, não precisa mais consultar por CoeId
            // Esta ação agora apenas atualiza as posições com base nos dados fornecidos
            var posicoes = await _context.PosicoesCliente
                .Where(p => p.CoeId == dados!.CoeId && p.Ativo)
                .ToListAsync();

            var posicoesAtualizadas = new List<Guid>();

            foreach (var posicao in posicoes)
            {
                var valorAnterior = posicao.ValorAtual;
                // Atualizar com base no valor fornecido nos dados
                posicao.ValorAtual = dados.ValorMtm;
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
                    ValorMtm = dados.ValorMtm
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
            var dados = JsonSerializer.Deserialize<DadosPosicao>(etapa.DadosEntrada ?? "{}");

            var posicoes = await _context.PosicoesCliente
                .Where(p => p.CoeId == dados!.CoeId && p.Ativo)
                .ToListAsync();

            foreach (var posicao in posicoes)
            {
                // Reverter para valor investido original
                posicao.ValorAtual = posicao.ValorInvestido;
                posicao.DataAtualizacao = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Posições revertidas para valor investido");

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
        public decimal ValorMtm { get; set; }
    }
}
