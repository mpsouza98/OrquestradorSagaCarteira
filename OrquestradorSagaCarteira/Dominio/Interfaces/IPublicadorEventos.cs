namespace OrquestradorSagaCarteira.Dominio.Interfaces;

/// <summary>
/// Interface para o publicador de eventos (Subject do padrão Observer)
/// </summary>
public interface IPublicadorEventos
{
    Task PublicarAsync(string topico, string tipoEvento, object dadosEvento);
    Task InscreverObservadorAsync(string topico, IObservador observador);
    Task RemoverObservadorAsync(string topico, string nomeObservador);
    Task NotificarObservadoresAsync(string topico, string dadosEvento);
}

