 using OrquestradorSagaCarteira.Dominio.Interfaces;

namespace OrquestradorSagaCarteira.Aplicacao.Servicos;

public class PublicadorEventos : IPublicadorEventos
{
    private readonly Dictionary<string, List<IObservador>> _observadores = new();
    private readonly ILogger<PublicadorEventos> _logger;

    public PublicadorEventos(ILogger<PublicadorEventos> logger)
    {
        _logger = logger;
    }

    public async Task PublicarAsync(string topico, string dadosEvento)
    {
        _logger.LogInformation("Publicando evento no tópico {Topico}", topico);
        await NotificarObservadoresAsync(topico, dadosEvento);
    }

    public async Task InscreverObservadorAsync(string topico, IObservador observador)
    {
        if (!_observadores.TryGetValue(topico, out var lista))
        {
            lista = new List<IObservador>();
            _observadores[topico] = lista;
        }

        if (!lista.Any(o => o.NomeObservador == observador.NomeObservador))
        {
            lista.Add(observador);
            _logger.LogInformation("Observador {Nome} inscrito no tópico {Topico}", observador.NomeObservador, topico);
        }

        await Task.CompletedTask;
    }

    public async Task RemoverObservadorAsync(string topico, string nomeObservador)
    {
        if (_observadores.TryGetValue(topico, out var lista))
        {
            lista.RemoveAll(o => o.NomeObservador == nomeObservador);
            _logger.LogInformation("Observador {Nome} removido do tópico {Topico}", nomeObservador, topico);
        }

        await Task.CompletedTask;
    }

    public async Task NotificarObservadoresAsync(string topico, string dadosEvento)
    {
        if (_observadores.TryGetValue(topico, out var lista) && lista.Count > 0)
        {
            var tarefas = lista.Select(obs => obs.NotificarAsync(topico, dadosEvento));
            await Task.WhenAll(tarefas);
            _logger.LogInformation("Notificados {Qtd} observadores do tópico {Topico}", lista.Count, topico);
        }
    }
}
