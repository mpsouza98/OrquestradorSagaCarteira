namespace OrquestradorSagaCarteira.Dominio.Enums;

public enum EstadoSaga
{
    Iniciada,
    EmExecucao,
    Concluida,
    Compensando,
    Compensada,
    Falhou
}

