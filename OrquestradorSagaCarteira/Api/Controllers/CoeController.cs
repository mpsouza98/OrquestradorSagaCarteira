using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;

namespace OrquestradorSagaCarteira.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoeController : ControllerBase
{
    private readonly CarteiraDbContext _context;

    public CoeController(CarteiraDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lista todos os COEs
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListarCoes()
    {
        var coes = await _context.Coes
            .Include(c => c.Ativos)
            .Include(c => c.Barreiras)
            .Where(c => c.Ativo)
            .ToListAsync();

        return Ok(coes);
    }

    /// <summary>
    /// Obtém um COE por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> ObterCoe(Guid id)
    {
        var coe = await _context.Coes
            .Include(c => c.Ativos)
            .Include(c => c.Barreiras)
            .Include(c => c.Posicoes)
            .Include(c => c.Mtms)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (coe == null)
            return NotFound();

        return Ok(coe);
    }

    /// <summary>
    /// Obtém MTMs de um COE (agora pré-calculados na base)
    /// </summary>
    [HttpGet("{id}/mtms")]
    public async Task<IActionResult> ObterMtms(int codigoOperacao)
    {
        var mtms = await _context.Mtms
            .Where(m => m.CodigoOperacao == codigoOperacao)
            .OrderByDescending(m => m.DataReferencia)
            .Take(30)
            .ToListAsync();

        return Ok(mtms);
    }

    /// <summary>
    /// Obtém posições de clientes de um COE
    /// </summary>
    [HttpGet("{id}/posicoes")]
    public async Task<IActionResult> ObterPosicoes(Guid id)
    {
        var posicoes = await _context.PosicoesCliente
            .Where(p => p.CoeId == id && p.Ativo)
            .ToListAsync();

        return Ok(posicoes);
    }

    /// <summary>
    /// Obtém barreiras de um COE
    /// </summary>
    [HttpGet("{id}/barreiras")]
    public async Task<IActionResult> ObterBarreiras(Guid id)
    {
        var barreiras = await _context.Barreiras
            .Where(b => b.CoeId == id && b.Ativa)
            .ToListAsync();

        return Ok(barreiras);
    }
}
