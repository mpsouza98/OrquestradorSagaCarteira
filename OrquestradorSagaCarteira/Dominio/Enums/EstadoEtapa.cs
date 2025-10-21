namespace OrquestradorSagaCarteira.Dominio.Enums;

public enum EstadoEtapa
{
    Pendente,
    EmExecucao,
    Concluida,
    Falhou,
    Compensando,
    Compensada,
    FalhaCompensacao
}

