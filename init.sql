-- Script de inicialização do banco de dados para Orquestrador Saga Carteira COE
-- MySQL 8.0

CREATE DATABASE IF NOT EXISTS saga_carteira_coe;
USE saga_carteira_coe;

-- Tabela de Sagas
CREATE TABLE IF NOT EXISTS saga (
    id CHAR(36) PRIMARY KEY,
    tipo_saga VARCHAR(50) NOT NULL,
    estado_saga VARCHAR(50) NOT NULL,
    data_criacao DATETIME NOT NULL,
    data_atualizacao DATETIME NOT NULL,
    data_finalizacao DATETIME NULL,
    dados_contexto JSON,
    mensagem_erro TEXT NULL,
    INDEX idx_estado_saga (estado_saga),
    INDEX idx_tipo_saga (tipo_saga),
    INDEX idx_data_criacao (data_criacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Etapas da Saga
CREATE TABLE IF NOT EXISTS etapa_saga (
    id CHAR(36) PRIMARY KEY,
    saga_id CHAR(36) NOT NULL,
    nome_etapa VARCHAR(100) NOT NULL,
    ordem_execucao INT NOT NULL,
    estado_etapa VARCHAR(50) NOT NULL,
    tipo_acao VARCHAR(50) NOT NULL,
    data_inicio DATETIME NULL,
    data_finalizacao DATETIME NULL,
    dados_entrada JSON,
    dados_saida JSON,
    mensagem_erro TEXT NULL,
    tentativas INT DEFAULT 0,
    FOREIGN KEY (saga_id) REFERENCES saga(id) ON DELETE CASCADE,
    INDEX idx_saga_id (saga_id),
    INDEX idx_estado_etapa (estado_etapa),
    INDEX idx_ordem_execucao (ordem_execucao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de COE (Certificado de Operações Estruturadas)
CREATE TABLE IF NOT EXISTS coe (
    id CHAR(36) PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL UNIQUE,
    descricao VARCHAR(255),
    data_emissao DATE NOT NULL,
    data_vencimento DATE NOT NULL,
    valor_nominal DECIMAL(18, 2) NOT NULL,
    percentual_renda_fixa DECIMAL(5, 2) NOT NULL,
    percentual_renda_variavel DECIMAL(5, 2) NOT NULL,
    tipo_estrutura VARCHAR(50) NOT NULL,
    ativo BOOLEAN DEFAULT TRUE,
    data_criacao DATETIME NOT NULL,
    data_atualizacao DATETIME NOT NULL,
    INDEX idx_codigo (codigo),
    INDEX idx_ativo (ativo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Ativos do COE
CREATE TABLE IF NOT EXISTS ativo_coe (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    codigo_ativo VARCHAR(50) NOT NULL,
    tipo_ativo VARCHAR(50) NOT NULL,
    percentual_participacao DECIMAL(5, 2) NOT NULL,
    quantidade DECIMAL(18, 4),
    preco_inicial DECIMAL(18, 4),
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    INDEX idx_coe_id (coe_id),
    INDEX idx_codigo_ativo (codigo_ativo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Barreiras
CREATE TABLE IF NOT EXISTS barreira (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    tipo_barreira VARCHAR(50) NOT NULL, -- AUTOCALL, BEST_OF, WORST_OF
    nivel_barreira DECIMAL(18, 4) NOT NULL,
    data_observacao DATE NOT NULL,
    atingida BOOLEAN DEFAULT FALSE,
    data_atingimento DATETIME NULL,
    valor_atingimento DECIMAL(18, 4) NULL,
    ativa BOOLEAN DEFAULT TRUE,
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    INDEX idx_coe_id (coe_id),
    INDEX idx_tipo_barreira (tipo_barreira),
    INDEX idx_atingida (atingida),
    INDEX idx_data_observacao (data_observacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Cotações
CREATE TABLE IF NOT EXISTS cotacao (
    id CHAR(36) PRIMARY KEY,
    codigo_ativo VARCHAR(50) NOT NULL,
    tipo_ativo VARCHAR(50) NOT NULL,
    data_referencia DATE NOT NULL,
    preco_abertura DECIMAL(18, 4),
    preco_fechamento DECIMAL(18, 4),
    preco_maximo DECIMAL(18, 4),
    preco_minimo DECIMAL(18, 4),
    volume DECIMAL(18, 2),
    data_criacao DATETIME NOT NULL,
    UNIQUE KEY uk_cotacao (codigo_ativo, data_referencia),
    INDEX idx_codigo_ativo (codigo_ativo),
    INDEX idx_data_referencia (data_referencia)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de MTM (Mark-to-Market)
CREATE TABLE IF NOT EXISTS mtm (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    data_referencia DATE NOT NULL,
    valor_renda_fixa DECIMAL(18, 2),
    valor_renda_variavel DECIMAL(18, 2),
    valor_total DECIMAL(18, 2) NOT NULL,
    percentual_rentabilidade DECIMAL(10, 4),
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    UNIQUE KEY uk_mtm (coe_id, data_referencia),
    INDEX idx_coe_id (coe_id),
    INDEX idx_data_referencia (data_referencia)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Posições de Cliente
CREATE TABLE IF NOT EXISTS posicao_cliente (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    codigo_cliente VARCHAR(50) NOT NULL,
    quantidade DECIMAL(18, 4) NOT NULL,
    valor_investido DECIMAL(18, 2) NOT NULL,
    valor_atual DECIMAL(18, 2),
    data_aquisicao DATE NOT NULL,
    data_atualizacao DATETIME NOT NULL,
    ativo BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    INDEX idx_coe_id (coe_id),
    INDEX idx_codigo_cliente (codigo_cliente),
    INDEX idx_ativo (ativo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Valorização Contábil
CREATE TABLE IF NOT EXISTS valorizacao_contabil (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    data_referencia DATE NOT NULL,
    valor_contabil DECIMAL(18, 2) NOT NULL,
    valor_mercado DECIMAL(18, 2) NOT NULL,
    diferenca DECIMAL(18, 2) NOT NULL,
    ajuste_contabil DECIMAL(18, 2),
    conta_debito VARCHAR(50),
    conta_credito VARCHAR(50),
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    UNIQUE KEY uk_valorizacao (coe_id, data_referencia),
    INDEX idx_coe_id (coe_id),
    INDEX idx_data_referencia (data_referencia)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Eventos Corporativos
CREATE TABLE IF NOT EXISTS evento_corporativo (
    id CHAR(36) PRIMARY KEY,
    codigo_ativo VARCHAR(50) NOT NULL,
    tipo_evento VARCHAR(50) NOT NULL, -- SPLIT, INPLIT, CREDITO, DIVIDENDO
    data_evento DATE NOT NULL,
    data_com DATE,
    fator_ajuste DECIMAL(18, 8),
    descricao TEXT,
    processado BOOLEAN DEFAULT FALSE,
    data_processamento DATETIME NULL,
    data_criacao DATETIME NOT NULL,
    INDEX idx_codigo_ativo (codigo_ativo),
    INDEX idx_tipo_evento (tipo_evento),
    INDEX idx_processado (processado),
    INDEX idx_data_evento (data_evento)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Liquidações (Autocall)
CREATE TABLE IF NOT EXISTS liquidacao (
    id CHAR(36) PRIMARY KEY,
    coe_id CHAR(36) NOT NULL,
    barreira_id CHAR(36),
    tipo_liquidacao VARCHAR(50) NOT NULL, -- AUTOCALL, VENCIMENTO, ANTECIPADO
    data_liquidacao DATE NOT NULL,
    valor_liquidacao DECIMAL(18, 2) NOT NULL,
    percentual_retorno DECIMAL(10, 4),
    motivo TEXT,
    status VARCHAR(50) NOT NULL,
    data_criacao DATETIME NOT NULL,
    data_processamento DATETIME NULL,
    FOREIGN KEY (coe_id) REFERENCES coe(id) ON DELETE CASCADE,
    FOREIGN KEY (barreira_id) REFERENCES barreira(id) ON DELETE SET NULL,
    INDEX idx_coe_id (coe_id),
    INDEX idx_status (status),
    INDEX idx_data_liquidacao (data_liquidacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Histórico de Compensações
CREATE TABLE IF NOT EXISTS historico_compensacao (
    id CHAR(36) PRIMARY KEY,
    etapa_saga_id CHAR(36) NOT NULL,
    tipo_compensacao VARCHAR(100) NOT NULL,
    estado_compensacao VARCHAR(50) NOT NULL,
    dados_compensacao JSON,
    data_inicio DATETIME NOT NULL,
    data_finalizacao DATETIME NULL,
    mensagem_erro TEXT NULL,
    FOREIGN KEY (etapa_saga_id) REFERENCES etapa_saga(id) ON DELETE CASCADE,
    INDEX idx_etapa_saga_id (etapa_saga_id),
    INDEX idx_estado_compensacao (estado_compensacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Eventos do Sistema (Observer Pattern)
CREATE TABLE IF NOT EXISTS evento_sistema (
    id CHAR(36) PRIMARY KEY,
    tipo_evento VARCHAR(100) NOT NULL,
    topico VARCHAR(100) NOT NULL,
    dados_evento JSON NOT NULL,
    data_publicacao DATETIME NOT NULL,
    processado BOOLEAN DEFAULT FALSE,
    data_processamento DATETIME NULL,
    INDEX idx_tipo_evento (tipo_evento),
    INDEX idx_topico (topico),
    INDEX idx_processado (processado),
    INDEX idx_data_publicacao (data_publicacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Inscrições de Observadores
CREATE TABLE IF NOT EXISTS inscricao_observador (
    id CHAR(36) PRIMARY KEY,
    topico VARCHAR(100) NOT NULL,
    nome_observador VARCHAR(100) NOT NULL,
    ativo BOOLEAN DEFAULT TRUE,
    data_criacao DATETIME NOT NULL,
    INDEX idx_topico (topico),
    INDEX idx_ativo (ativo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Inserir dados de exemplo
INSERT INTO coe (id, codigo, descricao, data_emissao, data_vencimento, valor_nominal, 
                 percentual_renda_fixa, percentual_renda_variavel, tipo_estrutura, 
                 ativo, data_criacao, data_atualizacao)
VALUES 
    (UUID(), 'COE001', 'COE Ibovespa com Autocall', '2025-01-15', '2026-01-15', 100000.00, 
     70.00, 30.00, 'AUTOCALL', TRUE, NOW(), NOW()),
    (UUID(), 'COE002', 'COE Multiativos Best-of', '2025-02-01', '2027-02-01', 250000.00, 
     60.00, 40.00, 'BEST_OF', TRUE, NOW(), NOW());

