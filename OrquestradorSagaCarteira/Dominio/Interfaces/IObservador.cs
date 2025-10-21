namespace OrquestradorSagaCarteira.Dominio.Interfaces;

/// <summary>
/// Interface para o padrão Observer
/// </summary>
public interface IObservador
{
    string NomeObservador { get; }
    Task NotificarAsync(string topico, string dadosEvento);
}

