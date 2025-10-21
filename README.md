# Orquestrador Saga Carteira COE

Sistema de gestão de eventos para carteira de COE (Certificado de Operações Estruturadas) utilizando o padrão **Saga** para orquestração de transações distribuídas e o padrão **Observer** para processamento de eventos.

## 🎯 Arquitetura

### Padrão Saga Implementado

- **Orquestração centralizada**: O `OrquestradorSaga` coordena todas as etapas
- **Ações compensatórias**: Cada ação possui sua compensação para rollback
- **Estado persistente**: Todas as sagas e etapas são persistidas no MySQL
- **Idempotência**: Ações podem ser reexecutadas sem efeitos colaterais

### Padrão Observer Implementado

- **Publicador de Eventos**: Gerencia publicação e notificação de eventos
- **Observadores especializados**: Respondem a eventos específicos (Cotação, MTM, Barreira, Eventos Corporativos)
- **Tópicos**: Sistema de tópicos para categorizar eventos

## 📊 Estrutura do Banco de Dados

O script `init.sql` contém todas as tabelas necessárias:

- **saga**: Registro das sagas executadas
- **etapa_saga**: Etapas de cada saga
- **coe**: Certificados de Operações Estruturadas
- **ativo_coe**: Ativos que compõem cada COE
- **barreira**: Barreiras (Autocall, Best-of, Worst-of)
- **cotacao**: Cotações de ativos
- **mtm**: Mark-to-Market
- **posicao_cliente**: Posições dos clientes
- **valorizacao_contabil**: Valorização contábil
- **evento_corporativo**: Eventos corporativos (Split, Insplit, etc)
- **liquidacao**: Liquidações (Autocall)
- **historico_compensacao**: Histórico de compensações
- **evento_sistema**: Eventos publicados
- **inscricao_observador**: Inscrições de observadores em tópicos

## 🔄 Fluxos de Saga Implementados

### 1. Valorização MTM
```
Calcular MTM Renda Fixa 
  → Calcular MTM Renda Variável 
  → Consolidar MTM 
  → Calcular Valorização Contábil 
  → Atualizar Posição Cliente
```

### 2. Ativação Autocall
```
Verificar Barreira 
  → Processar Atingimento 
  → Iniciar Liquidação 
  → Calcular Valor Liquidação 
  → Liquidar Posições 
  → Encerrar COE
```

### 3. Processamento Evento Corporativo
```
Processar Split/Insplit 
  → Ajustar Posições 
  → Realizar Ajuste Contábil
```

### 4. Processamento Cotação
```
Processar Cotação
```

## 🚀 Endpoints da API

### Saga Controller

#### POST `/api/saga/valorizacao-mtm`
Inicia saga de valorização MTM
```json
{
  "coeId": "guid",
  "dataReferencia": "2025-10-21"
}
```

#### POST `/api/saga/autocall`
Inicia saga de autocall
```json
{
  "coeId": "guid",
  "barreiraId": "guid",
  "dataLiquidacao": "2025-10-21"
}
```

#### POST `/api/saga/evento-corporativo`
Inicia saga de evento corporativo
```json
{
  "eventoId": "guid",
  "tipoEvento": "Split"
}
```

#### POST `/api/saga/processar-cotacao`
Processa uma cotação
```json
{
  "codigoAtivo": "PETR4",
  "tipoAtivo": "ACAO",
  "dataReferencia": "2025-10-21",
  "precoFechamento": 38.50
}
```

#### GET `/api/saga/{id}`
Obtém detalhes de uma saga

#### GET `/api/saga?pagina=1&tamanhoPagina=50`
Lista sagas com paginação

#### POST `/api/saga/{id}/compensar`
Força compensação de uma saga

### Eventos Controller (Simulação de Consumers)

#### POST `/api/eventos/cotacao`
Publica evento de cotação
```json
{
  "codigoAtivo": "PETR4",
  "precoFechamento": 38.50,
  "dataReferencia": "2025-10-21"
}
```

#### POST `/api/eventos/mtm`
Publica evento de MTM
```json
{
  "coeId": "guid",
  "dataReferencia": "2025-10-21"
}
```

#### POST `/api/eventos/barreira`
Publica evento de barreira
```json
{
  "barreiraId": "guid",
  "coeId": "guid",
  "tipoBarreira": "Autocall",
  "dataObservacao": "2025-10-21"
}
```

#### POST `/api/eventos/evento-corporativo`
Publica evento corporativo
```json
{
  "eventoId": "guid",
  "tipoEvento": "Split",
  "codigoAtivo": "PETR4",
  "fatorAjuste": 2.0
}
```

### COE Controller

#### GET `/api/coe`
Lista todos os COEs ativos

#### GET `/api/coe/{id}`
Obtém detalhes de um COE

#### GET `/api/coe/{id}/mtms`
Obtém histórico de MTM

#### GET `/api/coe/{id}/posicoes`
Obtém posições de clientes

#### GET `/api/coe/{id}/barreiras`
Obtém barreiras do COE

## 🛠️ Configuração

### Pré-requisitos
- .NET 8.0
- MySQL 8.0

### Configuração do Banco de Dados

1. Executar o script `init.sql` no MySQL
2. Ajustar connection string em `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=saga_carteira_coe;Uid=root;Pwd=suasenha;"
  }
}
```

### Executar o Projeto

```bash
cd OrquestradorSagaCarteira
dotnet restore
dotnet run
```

A API estará disponível em `https://localhost:7XXX` e o Swagger em `https://localhost:7XXX/swagger`

## 🧪 Testando o Sistema

### 1. Publicar um evento de MTM (simula consumer)
```bash
POST /api/eventos/mtm
{
  "coeId": "guid-do-coe",
  "dataReferencia": "2025-10-21"
}
```

Isso irá:
- Publicar evento no tópico `topico.mtm`
- Observador MTM receberá o evento
- Saga de valorização será iniciada automaticamente
- 5 etapas serão executadas sequencialmente

### 2. Verificar status da saga
```bash
GET /api/saga
```

### 3. Forçar uma compensação (teste de rollback)
```bash
POST /api/saga/{sagaId}/compensar
```

## 📝 Conceitos Implementados

### Saga Pattern
- ✅ Orquestrador centralizado
- ✅ Etapas sequenciais
- ✅ Ações compensatórias
- ✅ Estado persistente
- ✅ Tratamento de erros
- ✅ Retry logic

### Observer Pattern
- ✅ Publicador de eventos
- ✅ Inscrição de observadores
- ✅ Notificação assíncrona
- ✅ Tópicos de eventos
- ✅ Desacoplamento

### Domínio COE
- ✅ Valorização MTM
- ✅ Autocall
- ✅ Eventos corporativos (Split/Insplit)
- ✅ Barreiras (Best-of, Worst-of)
- ✅ Posições de clientes
- ✅ Valorização contábil

## 📦 Estrutura do Projeto

```
OrquestradorSagaCarteira/
├── Dominio/
│   ├── Entidades/          # Entidades do domínio
│   ├── Enums/              # Enumerações
│   └── Interfaces/         # Interfaces do padrão Saga e Observer
├── Aplicacao/
│   ├── Acoes/              # Implementações das ações da saga
│   ├── Observadores/       # Implementações dos observadores
│   └── Servicos/           # Orquestrador e Publicador
├── Infraestrutura/
│   └── Persistencia/       # DbContext e configurações EF
└── Api/
    └── Controllers/        # Controllers REST
```

## 🔍 Logs

O sistema registra logs detalhados de todas as operações:
- Início e fim de sagas
- Execução de cada etapa
- Compensações realizadas
- Eventos publicados e recebidos
- Erros e exceções

## 🎓 Nomenclatura em Português

Conforme solicitado, todos os conceitos do padrão Saga estão em português:
- `Saga` (não "Saga Flow")
- `EtapaSaga` (não "Saga Step")
- `EstadoSaga` (não "Saga State")
- `OrquestradorSaga` (não "Saga Orchestrator")
- `AcaoSaga` (não "Saga Action")
- `Compensacao` (não "Compensation")
- `Observador` (não "Observer")
- `PublicadorEventos` (não "Event Publisher")


Faça as seguintes mudanças:

- Remova toda implementação de calculo de MTM, pois ele ja estara disponivel na base mysql. Reestruture o mtm para os seguintes campos: codigo_operacao (inteiro); valor_mtm (decimal); valor_accrual (decimal); sequencial_perna (boolean)
- Restruture o consumo de cotações, removendo toda implementação e seguindo a mesma estrategia do mtm, com os registros pre inseridos na base dada a estrutura da tabela: codigo_cotacao; ticker_ativo; fonte; data

Reestruture o GUIA_USO e as devidas orquestrações na base para validar a seguinte saga:

- Observação das cotações diarias no cesto de ativos vinculadas ao COE, sendo ela: META, Snowflak, OPen ai, Microsoft.
- Observer para cada cotação, onde o observer é cadastrado por barreira e condição (UP ou DOWN). Comanda a mudança de estado para barreira atingida
- Observer para autocall. Observa as barreiras atingidas, mapeia e agrega 1:N para os COEs em caso de worst-off ou best off para cesta. Para exemplo, use o caso de worst-off onde a META é o ativo de menor taxa de variação, com cotacao_dia > cotacao_inicial. No caso do worst-off, todos os ativos atingem barreira. Em caso de autocall, comandar a liquidação da operação para data agendada previamente estabelecida na estrutura