using Microsoft.EntityFrameworkCore;
using OrquestradorSagaCarteira.Dominio.Entidades;
using OrquestradorSagaCarteira.Dominio.Interfaces;
using OrquestradorSagaCarteira.Infraestrutura.Persistencia;
using System.Text.Json;

namespace OrquestradorSagaCarteira.Aplicacao.Servicos;

public class PublicadorEventos : IPublicadorEventos
{
    private readonly CarteiraDbContext _context;
    private readonly Dictionary<string, List<IObservador>> _observadores;
    private readonly ILogger<PublicadorEventos> _logger;

    public PublicadorEventos(CarteiraDbContext context, ILogger<PublicadorEventos> logger)
    {
        _context = context;
        _logger = logger;
        _observadores = new Dictionary<string, List<IObservador>>();
    }

    public async Task PublicarAsync(string topico, string tipoEvento, object dadosEvento)
    {
        var evento = new EventoSistema
        {
            Id = Guid.NewGuid(),
            TipoEvento = tipoEvento,
            Topico = topico,
            DadosEvento = JsonSerializer.Serialize(dadosEvento),
            DataPublicacao = DateTime.UtcNow,
            Processado = false
        };

        _context.EventosSistema.Add(evento);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Evento {TipoEvento} publicado no tópico {Topico}", tipoEvento, topico);

        // Notificar observadores
        await NotificarObservadoresAsync(topico, evento.DadosEvento);
    }

    public async Task InscreverObservadorAsync(string topico, IObservador observador)
    {
        if (!_observadores.ContainsKey(topico))
        {
            _observadores[topico] = new List<IObservador>();
        }

        if (!_observadores[topico].Any(o => o.NomeObservador == observador.NomeObservador))
        {
            _observadores[topico].Add(observador);

            // Persistir inscrição
            var inscricao = new InscricaoObservador
            {
                Id = Guid.NewGuid(),
                Topico = topico,
                NomeObservador = observador.NomeObservador,
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            };

            _context.InscricoesObservador.Add(inscricao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Observador {NomeObservador} inscrito no tópico {Topico}",
                observador.NomeObservador, topico);
        }
    }

    public async Task RemoverObservadorAsync(string topico, string nomeObservador)
    {
        if (_observadores.ContainsKey(topico))
        {
            _observadores[topico].RemoveAll(o => o.NomeObservador == nomeObservador);

            // Desativar inscrição
            var inscricao = await _context.InscricoesObservador
                .FirstOrDefaultAsync(i => i.Topico == topico && i.NomeObservador == nomeObservador && i.Ativo);

            if (inscricao != null)
            {
                inscricao.Ativo = false;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Observador {NomeObservador} removido do tópico {Topico}",
                nomeObservador, topico);
        }
    }

    public async Task NotificarObservadoresAsync(string topico, string dadosEvento)
    {
        if (_observadores.ContainsKey(topico))
        {
            var tarefas = _observadores[topico]
                .Select(observador => observador.NotificarAsync(topico, dadosEvento));

            await Task.WhenAll(tarefas);

            _logger.LogInformation("Notificados {NumObservadores} observadores do tópico {Topico}",
                _observadores[topico].Count, topico);
        }
    }
}

