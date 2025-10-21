namespace OrquestradorSagaCarteira.Dominio.Enums;

public enum TipoAcao
{
    // Ações de Cotação
    ProcessarCotacao,
    
    // Ações de MTM
    CalcularMtmRendaFixa,
    CalcularMtmRendaVariavel,
    ConsolidarMtm,
    
    // Ações de Valorização
    CalcularValorizacaoContabil,
    AtualizarPosicaoCliente,
    
    // Ações de Evento Corporativo
    ProcessarSplit,
    ProcessarInsplit,
    AjustarPosicoes,
    
    // Ações de Barreira
    VerificarBarreira,
    ProcessarAtingimentoBarreira,
    
    // Ações de Autocall
    IniciarLiquidacao,
    CalcularValorLiquidacao,
    LiquidarPosicoes,
    EncerrarCoe,
    
    // Ações Contábeis
    LancarContabilidade,
    RealizarAjusteContabil
}

