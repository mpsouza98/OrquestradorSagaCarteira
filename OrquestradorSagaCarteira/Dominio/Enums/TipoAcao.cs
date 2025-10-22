namespace OrquestradorSagaCarteira.Dominio.Enums;

public enum TipoAcao
{
    // Ações de Barreira
    VerificarBarreira,
    PersistirBarreira,
    NotificarBarreiraAtingida,
    
    // Ações de AutoCall
    AgregarCesta,
    VerificarAutoCall,
    PersistirAutoCall,
    NotificarAutoCall,
    
    // Ações de Liquidação
    AgendarDesfazimentoOperacao
}
