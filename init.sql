-- Script de inicialização do banco de dados para Orquestrador Saga Autocall
-- MySQL 8.0

CREATE DATABASE IF NOT EXISTS saga_autocall;
USE saga_autocall;

-- ==========================
-- TABELAS PRINCIPAIS
-- ==========================

-- Tabela de Operações (instâncias dentro da saga)
CREATE TABLE IF NOT EXISTS operacao (
    id CHAR(36) PRIMARY KEY,
    codigo_operacao VARCHAR(50) NOT NULL UNIQUE,
    descricao VARCHAR(255),
    data_criacao DATETIME NOT NULL,
    data_vencimento DATE NOT NULL,
    valor_nominal DECIMAL(18, 2) NOT NULL,
    tipo_estrutura VARCHAR(50) NOT NULL, -- BestOf, WorstOf
    ativa BOOLEAN DEFAULT TRUE,
    data_atualizacao DATETIME NOT NULL,
    INDEX idx_codigo_operacao (codigo_operacao),
    INDEX idx_ativa (ativa)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Ativos da Operação
CREATE TABLE IF NOT EXISTS ativo_operacao (
    id CHAR(36) PRIMARY KEY,
    operacao_id CHAR(36) NOT NULL,
    ticker VARCHAR(50) NOT NULL,
    cotacao_inicial DECIMAL(18, 4) NOT NULL,
    percentual_participacao DECIMAL(5, 2) NOT NULL,
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (operacao_id) REFERENCES operacao(id) ON DELETE CASCADE,
    INDEX idx_operacao_id (operacao_id),
    INDEX idx_ticker (ticker)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Barreiras da Operação
CREATE TABLE IF NOT EXISTS barreira_operacao (
    id CHAR(36) PRIMARY KEY,
    operacao_id CHAR(36) NOT NULL,
    ticker VARCHAR(50) NULL, -- NULL significa barreira de cesta
    tipo_barreira VARCHAR(50) NOT NULL, -- Autocall, KnockIn, KnockOut
    condicao VARCHAR(10) NOT NULL, -- UP ou DOWN
    nivel_barreira DECIMAL(18, 4) NOT NULL,
    data_observacao DATE NOT NULL,
    atingida BOOLEAN DEFAULT FALSE,
    data_atingimento DATETIME NULL,
    valor_atingimento DECIMAL(18, 4) NULL,
    ativa BOOLEAN DEFAULT TRUE,
    data_criacao DATETIME NOT NULL,
    FOREIGN KEY (operacao_id) REFERENCES operacao(id) ON DELETE CASCADE,
    INDEX idx_operacao_id (operacao_id),
    INDEX idx_ticker (ticker),
    INDEX idx_tipo_barreira (tipo_barreira),
    INDEX idx_atingida (atingida),
    INDEX idx_data_observacao (data_observacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Cotações
CREATE TABLE IF NOT EXISTS cotacao (
    id CHAR(36) PRIMARY KEY,
    ticker_ativo VARCHAR(50) NOT NULL,
    fonte VARCHAR(50) NOT NULL,
    data DATE NOT NULL,
    preco_fechamento DECIMAL(18, 4) NOT NULL,
    data_criacao DATETIME NOT NULL,
    UNIQUE KEY uk_cotacao (ticker_ativo, data, fonte),
    INDEX idx_ticker_ativo (ticker_ativo),
    INDEX idx_data (data)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Eventos de Barreira
CREATE TABLE IF NOT EXISTS evento_barreira (
    id CHAR(36) PRIMARY KEY,
    barreira_id CHAR(36) NOT NULL,
    operacao_id CHAR(36) NOT NULL,
    ticker VARCHAR(50) NULL,
    valor_observado DECIMAL(18, 4) NOT NULL,
    nivel_barreira DECIMAL(18, 4) NOT NULL,
    tipo_barreira VARCHAR(50) NOT NULL,
    data_evento DATETIME NOT NULL,
    dados_evento JSON,
    INDEX idx_barreira_id (barreira_id),
    INDEX idx_operacao_id (operacao_id),
    INDEX idx_data_evento (data_evento)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Eventos de Autocall
CREATE TABLE IF NOT EXISTS evento_autocall (
    id CHAR(36) PRIMARY KEY,
    operacao_id CHAR(36) NOT NULL,
    autocall_atingido BOOLEAN NOT NULL,
    valor_cesta DECIMAL(18, 4) NULL,
    tipo_estrutura VARCHAR(50) NOT NULL,
    data_evento DATETIME NOT NULL,
    dados_evento JSON,
    INDEX idx_operacao_id (operacao_id),
    INDEX idx_autocall_atingido (autocall_atingido),
    INDEX idx_data_evento (data_evento)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Tabela de Liquidações Agendadas
CREATE TABLE IF NOT EXISTS liquidacao_agendada (
    id CHAR(36) PRIMARY KEY,
    operacao_id CHAR(36) NOT NULL,
    data_agendamento DATETIME NOT NULL,
    data_liquidacao DATE NOT NULL,
    valor_liquidacao DECIMAL(18, 2) NOT NULL,
    status VARCHAR(50) NOT NULL, -- Agendada, Processada, Cancelada
    motivo TEXT,
    data_processamento DATETIME NULL,
    dados_liquidacao JSON,
    INDEX idx_operacao_id (operacao_id),
    INDEX idx_status (status),
    INDEX idx_data_liquidacao (data_liquidacao)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==========================
-- TABELAS DE SAGA
-- ==========================

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

-- ==========================
-- DADOS INICIAIS (DML)
-- ==========================

-- Inserir operações de exemplo
INSERT INTO operacao (id, codigo_operacao, descricao, data_criacao, data_vencimento, valor_nominal, tipo_estrutura, ativa, data_atualizacao)
VALUES 
    (UUID(), 'AUTOCALL-001', 'Operação Autocall Best-Of 3 Ações', NOW(), DATE_ADD(CURDATE(), INTERVAL 12 MONTH), 100000.00, 'BestOf', TRUE, NOW()),
    (UUID(), 'AUTOCALL-002', 'Operação Autocall Worst-Of 2 Índices', NOW(), DATE_ADD(CURDATE(), INTERVAL 24 MONTH), 250000.00, 'WorstOf', TRUE, NOW());

-- Obter IDs das operações para uso posterior
SET @op1_id = (SELECT id FROM operacao WHERE codigo_operacao = 'AUTOCALL-001');
SET @op2_id = (SELECT id FROM operacao WHERE codigo_operacao = 'AUTOCALL-002');

-- Inserir ativos para AUTOCALL-001 (Best-Of)
INSERT INTO ativo_operacao (id, operacao_id, ticker, cotacao_inicial, percentual_participacao, data_criacao)
VALUES 
    (UUID(), @op1_id, 'PETR4', 35.50, 33.33, NOW()),
    (UUID(), @op1_id, 'VALE3', 68.20, 33.33, NOW()),
    (UUID(), @op1_id, 'ITUB4', 28.75, 33.34, NOW());

-- Inserir ativos para AUTOCALL-002 (Worst-Of)
INSERT INTO ativo_operacao (id, operacao_id, ticker, cotacao_inicial, percentual_participacao, data_criacao)
VALUES 
    (UUID(), @op2_id, 'IBOV', 125000.00, 50.00, NOW()),
    (UUID(), @op2_id, 'IFIX', 3200.00, 50.00, NOW());

-- Inserir barreiras para AUTOCALL-001
INSERT INTO barreira_operacao (id, operacao_id, ticker, tipo_barreira, condicao, nivel_barreira, data_observacao, atingida, ativa, data_criacao)
VALUES 
    -- Barreiras individuais de ativos (UP = apreciação)
    (UUID(), @op1_id, 'PETR4', 'KnockIn', 'UP', 10.00, DATE_ADD(CURDATE(), INTERVAL 3 MONTH), FALSE, TRUE, NOW()),
    (UUID(), @op1_id, 'VALE3', 'KnockIn', 'UP', 10.00, DATE_ADD(CURDATE(), INTERVAL 3 MONTH), FALSE, TRUE, NOW()),
    (UUID(), @op1_id, 'ITUB4', 'KnockIn', 'UP', 10.00, DATE_ADD(CURDATE(), INTERVAL 3 MONTH), FALSE, TRUE, NOW()),
    -- Barreira de Autocall da cesta (NULL no ticker = barreira de cesta)
    (UUID(), @op1_id, NULL, 'Autocall', 'UP', 15.00, DATE_ADD(CURDATE(), INTERVAL 6 MONTH), FALSE, TRUE, NOW());

-- Inserir barreiras para AUTOCALL-002
INSERT INTO barreira_operacao (id, operacao_id, ticker, tipo_barreira, condicao, nivel_barreira, data_observacao, atingida, ativa, data_criacao)
VALUES 
    -- Barreiras individuais
    (UUID(), @op2_id, 'IBOV', 'KnockIn', 'UP', 8.00, DATE_ADD(CURDATE(), INTERVAL 6 MONTH), FALSE, TRUE, NOW()),
    (UUID(), @op2_id, 'IFIX', 'KnockIn', 'UP', 8.00, DATE_ADD(CURDATE(), INTERVAL 6 MONTH), FALSE, TRUE, NOW()),
    -- Barreira de Autocall da cesta
    (UUID(), @op2_id, NULL, 'Autocall', 'UP', 12.00, DATE_ADD(CURDATE(), INTERVAL 12 MONTH), FALSE, TRUE, NOW());

-- Inserir cotações iniciais de exemplo
INSERT INTO cotacao (id, ticker_ativo, fonte, data, preco_fechamento, data_criacao)
VALUES 
    (UUID(), 'PETR4', 'B3', CURDATE(), 35.50, NOW()),
    (UUID(), 'VALE3', 'B3', CURDATE(), 68.20, NOW()),
    (UUID(), 'ITUB4', 'B3', CURDATE(), 28.75, NOW()),
    (UUID(), 'IBOV', 'B3', CURDATE(), 125000.00, NOW()),
    (UUID(), 'IFIX', 'B3', CURDATE(), 3200.00, NOW());

-- Inserir cotações do dia seguinte (simulação de variação positiva)
INSERT INTO cotacao (id, ticker_ativo, fonte, data, preco_fechamento, data_criacao)
VALUES 
    (UUID(), 'PETR4', 'B3', DATE_ADD(CURDATE(), INTERVAL 1 DAY), 36.80, NOW()),
    (UUID(), 'VALE3', 'B3', DATE_ADD(CURDATE(), INTERVAL 1 DAY), 71.50, NOW()),
    (UUID(), 'ITUB4', 'B3', DATE_ADD(CURDATE(), INTERVAL 1 DAY), 29.90, NOW()),
    (UUID(), 'IBOV', 'B3', DATE_ADD(CURDATE(), INTERVAL 1 DAY), 128500.00, NOW()),
    (UUID(), 'IFIX', 'B3', DATE_ADD(CURDATE(), INTERVAL 1 DAY), 3280.00, NOW());

-- ==========================
-- VIEWS ÚTEIS
-- ==========================

-- View para visualizar operações com seus ativos e barreiras
CREATE OR REPLACE VIEW vw_operacoes_completas AS
SELECT 
    o.id AS operacao_id,
    o.codigo_operacao,
    o.descricao,
    o.tipo_estrutura,
    o.valor_nominal,
    o.data_vencimento,
    o.ativa,
    COUNT(DISTINCT a.id) AS total_ativos,
    COUNT(DISTINCT b.id) AS total_barreiras,
    COUNT(DISTINCT CASE WHEN b.atingida = TRUE THEN b.id END) AS barreiras_atingidas
FROM operacao o
LEFT JOIN ativo_operacao a ON o.id = a.operacao_id
LEFT JOIN barreira_operacao b ON o.id = b.operacao_id
GROUP BY o.id, o.codigo_operacao, o.descricao, o.tipo_estrutura, o.valor_nominal, o.data_vencimento, o.ativa;

-- View para acompanhamento de sagas
CREATE OR REPLACE VIEW vw_status_sagas AS
SELECT 
    s.id AS saga_id,
    s.tipo_saga,
    s.estado_saga,
    s.data_criacao,
    s.data_finalizacao,
    TIMESTAMPDIFF(SECOND, s.data_criacao, COALESCE(s.data_finalizacao, NOW())) AS duracao_segundos,
    COUNT(e.id) AS total_etapas,
    COUNT(CASE WHEN e.estado_etapa = 'Concluida' THEN 1 END) AS etapas_concluidas,
    COUNT(CASE WHEN e.estado_etapa = 'Falhou' THEN 1 END) AS etapas_falhas
FROM saga s
LEFT JOIN etapa_saga e ON s.id = e.saga_id
GROUP BY s.id, s.tipo_saga, s.estado_saga, s.data_criacao, s.data_finalizacao;

-- ==========================
-- PROCEDURES ÚTEIS
-- ==========================

DELIMITER //

-- Procedure para limpar dados de teste
CREATE PROCEDURE sp_limpar_dados_teste()
BEGIN
    DELETE FROM etapa_saga;
    DELETE FROM saga;
    DELETE FROM evento_autocall;
    DELETE FROM evento_barreira;
    DELETE FROM liquidacao_agendada;
    DELETE FROM cotacao WHERE data > DATE_ADD(CURDATE(), INTERVAL 1 DAY);
END //

-- Procedure para obter estatísticas do sistema
CREATE PROCEDURE sp_estatisticas_sistema()
BEGIN
    SELECT 
        'Operações Ativas' AS metrica,
        COUNT(*) AS valor
    FROM operacao
    WHERE ativa = TRUE
    
    UNION ALL
    
    SELECT 
        'Total de Cotações',
        COUNT(*)
    FROM cotacao
    
    UNION ALL
    
    SELECT 
        'Barreiras Atingidas',
        COUNT(*)
    FROM barreira_operacao
    WHERE atingida = TRUE
    
    UNION ALL
    
    SELECT 
        'Sagas Executadas',
        COUNT(*)
    FROM saga
    
    UNION ALL
    
    SELECT 
        'Liquidações Agendadas',
        COUNT(*)
    FROM liquidacao_agendada
    WHERE status = 'Agendada';
END //

DELIMITER ;

-- Mensagem final
SELECT '✅ Base de dados inicializada com sucesso!' AS status;
SELECT * FROM vw_operacoes_completas;

