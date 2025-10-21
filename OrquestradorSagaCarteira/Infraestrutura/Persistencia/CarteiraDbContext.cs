using Microsoft.EntityFrameworkCore;
using OrquestradorSagaCarteira.Dominio.Entidades;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia;

public class CarteiraDbContext : DbContext
{
    public CarteiraDbContext(DbContextOptions<CarteiraDbContext> options) : base(options)
    {
    }

    public DbSet<Saga> Sagas { get; set; }
    public DbSet<EtapaSaga> EtapasSaga { get; set; }
    public DbSet<Coe> Coes { get; set; }
    public DbSet<AtivoCoe> AtivosCoe { get; set; }
    public DbSet<Barreira> Barreiras { get; set; }
    public DbSet<Cotacao> Cotacoes { get; set; }
    public DbSet<Mtm> Mtms { get; set; }
    public DbSet<PosicaoCliente> PosicoesCliente { get; set; }
    public DbSet<ValorizacaoContabil> ValorizacoesContabeis { get; set; }
    public DbSet<EventoCorporativo> EventosCorporativos { get; set; }
    public DbSet<Liquidacao> Liquidacoes { get; set; }
    public DbSet<HistoricoCompensacao> HistoricosCompensacao { get; set; }
    public DbSet<EventoSistema> EventosSistema { get; set; }
    public DbSet<InscricaoObservador> InscricoesObservador { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração Saga
        modelBuilder.Entity<Saga>(entity =>
        {
            entity.ToTable("saga");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TipoSaga).HasColumnName("tipo_saga").HasConversion<string>();
            entity.Property(e => e.EstadoSaga).HasColumnName("estado_saga").HasConversion<string>();
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataAtualizacao).HasColumnName("data_atualizacao");
            entity.Property(e => e.DataFinalizacao).HasColumnName("data_finalizacao");
            entity.Property(e => e.DadosContexto).HasColumnName("dados_contexto");
            entity.Property(e => e.MensagemErro).HasColumnName("mensagem_erro");
        });

        // Configuração EtapaSaga
        modelBuilder.Entity<EtapaSaga>(entity =>
        {
            entity.ToTable("etapa_saga");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SagaId).HasColumnName("saga_id");
            entity.Property(e => e.NomeEtapa).HasColumnName("nome_etapa");
            entity.Property(e => e.OrdemExecucao).HasColumnName("ordem_execucao");
            entity.Property(e => e.EstadoEtapa).HasColumnName("estado_etapa").HasConversion<string>();
            entity.Property(e => e.TipoAcao).HasColumnName("tipo_acao").HasConversion<string>();
            entity.Property(e => e.DataInicio).HasColumnName("data_inicio");
            entity.Property(e => e.DataFinalizacao).HasColumnName("data_finalizacao");
            entity.Property(e => e.DadosEntrada).HasColumnName("dados_entrada");
            entity.Property(e => e.DadosSaida).HasColumnName("dados_saida");
            entity.Property(e => e.MensagemErro).HasColumnName("mensagem_erro");
            entity.Property(e => e.Tentativas).HasColumnName("tentativas");
            
            entity.HasOne(e => e.Saga)
                .WithMany(s => s.Etapas)
                .HasForeignKey(e => e.SagaId);
        });

        // Configuração COE
        modelBuilder.Entity<Coe>(entity =>
        {
            entity.ToTable("coe");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Codigo).HasColumnName("codigo");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.DataEmissao).HasColumnName("data_emissao");
            entity.Property(e => e.DataVencimento).HasColumnName("data_vencimento");
            entity.Property(e => e.ValorNominal).HasColumnName("valor_nominal").HasPrecision(18, 2);
            entity.Property(e => e.PercentualRendaFixa).HasColumnName("percentual_renda_fixa").HasPrecision(5, 2);
            entity.Property(e => e.PercentualRendaVariavel).HasColumnName("percentual_renda_variavel").HasPrecision(5, 2);
            entity.Property(e => e.TipoEstrutura).HasColumnName("tipo_estrutura");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataAtualizacao).HasColumnName("data_atualizacao");
        });

        // Configuração AtivoCoe
        modelBuilder.Entity<AtivoCoe>(entity =>
        {
            entity.ToTable("ativo_coe");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CoeId).HasColumnName("coe_id");
            entity.Property(e => e.TickerAtivo).HasColumnName("ticker_ativo");
            entity.Property(e => e.TipoAtivo).HasColumnName("tipo_ativo");
            entity.Property(e => e.PercentualParticipacao).HasColumnName("percentual_participacao").HasPrecision(5, 2);
            entity.Property(e => e.Quantidade).HasColumnName("quantidade").HasPrecision(18, 4);
            entity.Property(e => e.PrecoInicial).HasColumnName("preco_inicial").HasPrecision(18, 4);
            entity.Property(e => e.CotacaoInicial).HasColumnName("cotacao_inicial").HasPrecision(18, 4);
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            
            entity.HasOne(e => e.Coe)
                .WithMany(c => c.Ativos)
                .HasForeignKey(e => e.CoeId);
        });

        // Configuração Barreira
        modelBuilder.Entity<Barreira>(entity =>
        {
            entity.ToTable("barreira");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CoeId).HasColumnName("coe_id");
            entity.Property(e => e.TickerAtivo).HasColumnName("ticker_ativo");
            entity.Property(e => e.TipoBarreira).HasColumnName("tipo_barreira").HasConversion<string>();
            entity.Property(e => e.Condicao).HasColumnName("condicao");
            entity.Property(e => e.NivelBarreira).HasColumnName("nivel_barreira").HasPrecision(18, 4);
            entity.Property(e => e.DataObservacao).HasColumnName("data_observacao");
            entity.Property(e => e.Atingida).HasColumnName("atingida");
            entity.Property(e => e.DataAtingimento).HasColumnName("data_atingimento");
            entity.Property(e => e.ValorAtingimento).HasColumnName("valor_atingimento").HasPrecision(18, 4);
            entity.Property(e => e.Ativa).HasColumnName("ativa");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            
            entity.HasOne(e => e.Coe)
                .WithMany(c => c.Barreiras)
                .HasForeignKey(e => e.CoeId);
        });

        // Configuração Cotacao
        modelBuilder.Entity<Cotacao>(entity =>
        {
            entity.ToTable("cotacao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CodigoCotacao).HasColumnName("codigo_cotacao");
            entity.Property(e => e.TickerAtivo).HasColumnName("ticker_ativo");
            entity.Property(e => e.Fonte).HasColumnName("fonte");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.PrecoFechamento).HasColumnName("preco_fechamento").HasPrecision(18, 4);
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        // Configuração MTM
        modelBuilder.Entity<Mtm>(entity =>
        {
            entity.ToTable("mtm");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CodigoOperacao).HasColumnName("codigo_operacao");
            entity.Property(e => e.ValorMtm).HasColumnName("valor_mtm").HasPrecision(18, 2);
            entity.Property(e => e.ValorAccrual).HasColumnName("valor_accrual").HasPrecision(18, 2);
            entity.Property(e => e.SequencialPerna).HasColumnName("sequencial_perna");
            entity.Property(e => e.DataReferencia).HasColumnName("data_referencia");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        // Configuração PosicaoCliente
        modelBuilder.Entity<PosicaoCliente>(entity =>
        {
            entity.ToTable("posicao_cliente");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CoeId).HasColumnName("coe_id");
            entity.Property(e => e.CodigoCliente).HasColumnName("codigo_cliente");
            entity.Property(e => e.Quantidade).HasColumnName("quantidade").HasPrecision(18, 4);
            entity.Property(e => e.ValorInvestido).HasColumnName("valor_investido").HasPrecision(18, 2);
            entity.Property(e => e.ValorAtual).HasColumnName("valor_atual").HasPrecision(18, 2);
            entity.Property(e => e.DataAquisicao).HasColumnName("data_aquisicao");
            entity.Property(e => e.DataAtualizacao).HasColumnName("data_atualizacao");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            
            entity.HasOne(e => e.Coe)
                .WithMany(c => c.Posicoes)
                .HasForeignKey(e => e.CoeId);
        });

        // Configuração ValorizacaoContabil
        modelBuilder.Entity<ValorizacaoContabil>(entity =>
        {
            entity.ToTable("valorizacao_contabil");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CoeId).HasColumnName("coe_id");
            entity.Property(e => e.DataReferencia).HasColumnName("data_referencia");
            entity.Property(e => e.ValorContabil).HasColumnName("valor_contabil").HasPrecision(18, 2);
            entity.Property(e => e.ValorMercado).HasColumnName("valor_mercado").HasPrecision(18, 2);
            entity.Property(e => e.Diferenca).HasColumnName("diferenca").HasPrecision(18, 2);
            entity.Property(e => e.AjusteContabil).HasColumnName("ajuste_contabil").HasPrecision(18, 2);
            entity.Property(e => e.ContaDebito).HasColumnName("conta_debito");
            entity.Property(e => e.ContaCredito).HasColumnName("conta_credito");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            
            entity.HasOne(e => e.Coe)
                .WithMany(c => c.Valorizacoes)
                .HasForeignKey(e => e.CoeId);
        });

        // Configuração EventoCorporativo
        modelBuilder.Entity<EventoCorporativo>(entity =>
        {
            entity.ToTable("evento_corporativo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CodigoAtivo).HasColumnName("codigo_ativo");
            entity.Property(e => e.TipoEvento).HasColumnName("tipo_evento").HasConversion<string>();
            entity.Property(e => e.DataEvento).HasColumnName("data_evento");
            entity.Property(e => e.DataCom).HasColumnName("data_com");
            entity.Property(e => e.FatorAjuste).HasColumnName("fator_ajuste").HasPrecision(18, 8);
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.Processado).HasColumnName("processado");
            entity.Property(e => e.DataProcessamento).HasColumnName("data_processamento");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        // Configuração Liquidacao
        modelBuilder.Entity<Liquidacao>(entity =>
        {
            entity.ToTable("liquidacao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CoeId).HasColumnName("coe_id");
            entity.Property(e => e.BarreiraId).HasColumnName("barreira_id");
            entity.Property(e => e.TipoLiquidacao).HasColumnName("tipo_liquidacao");
            entity.Property(e => e.DataLiquidacao).HasColumnName("data_liquidacao");
            entity.Property(e => e.ValorLiquidacao).HasColumnName("valor_liquidacao").HasPrecision(18, 2);
            entity.Property(e => e.PercentualRetorno).HasColumnName("percentual_retorno").HasPrecision(10, 4);
            entity.Property(e => e.Motivo).HasColumnName("motivo");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataProcessamento).HasColumnName("data_processamento");
            
            entity.HasOne(e => e.Coe)
                .WithMany(c => c.Liquidacoes)
                .HasForeignKey(e => e.CoeId);
                
            entity.HasOne(e => e.Barreira)
                .WithMany()
                .HasForeignKey(e => e.BarreiraId);
        });

        // Configuração HistoricoCompensacao
        modelBuilder.Entity<HistoricoCompensacao>(entity =>
        {
            entity.ToTable("historico_compensacao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EtapaSagaId).HasColumnName("etapa_saga_id");
            entity.Property(e => e.TipoCompensacao).HasColumnName("tipo_compensacao");
            entity.Property(e => e.EstadoCompensacao).HasColumnName("estado_compensacao");
            entity.Property(e => e.DadosCompensacao).HasColumnName("dados_compensacao");
            entity.Property(e => e.DataInicio).HasColumnName("data_inicio");
            entity.Property(e => e.DataFinalizacao).HasColumnName("data_finalizacao");
            entity.Property(e => e.MensagemErro).HasColumnName("mensagem_erro");
            
            entity.HasOne(e => e.EtapaSaga)
                .WithMany(es => es.Compensacoes)
                .HasForeignKey(e => e.EtapaSagaId);
        });

        // Configuração EventoSistema
        modelBuilder.Entity<EventoSistema>(entity =>
        {
            entity.ToTable("evento_sistema");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TipoEvento).HasColumnName("tipo_evento");
            entity.Property(e => e.Topico).HasColumnName("topico");
            entity.Property(e => e.DadosEvento).HasColumnName("dados_evento");
            entity.Property(e => e.DataPublicacao).HasColumnName("data_publicacao");
            entity.Property(e => e.Processado).HasColumnName("processado");
            entity.Property(e => e.DataProcessamento).HasColumnName("data_processamento");
        });

        // Configuração InscricaoObservador
        modelBuilder.Entity<InscricaoObservador>(entity =>
        {
            entity.ToTable("inscricao_observador");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Topico).HasColumnName("topico");
            entity.Property(e => e.NomeObservador).HasColumnName("nome_observador");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });
    }
}
