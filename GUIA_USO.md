# Guia de Uso - Orquestrador Saga Carteira COE

## 🚀 Iniciando o Projeto

### 1. Configurar o Banco de Dados

Execute o script SQL no MySQL:

```bash
mysql -u root -p < init.sql
```

Ou execute manualmente o conteúdo do arquivo `init.sql` no MySQL Workbench/phpMyAdmin.

### 2. Ajustar a Connection String

Edite o arquivo `appsettings.json` com suas credenciais:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=saga_carteira_coe;Uid=root;Pwd=SUA_SENHA;"
  }
}
```

### 3. Restaurar Pacotes e Compilar

```bash
cd OrquestradorSagaCarteira
dotnet restore
dotnet build
```

### 4. Executar a Aplicação

```bash
dotnet run
```

A API estará disponível em:
- HTTPS: `https://localhost:7xxx`
- Swagger: `https://localhost:7xxx/swagger`

---

## 📊 Arquitetura Atualizada

### Mudanças Importantes

#### MTM (Mark-to-Market)
- ✅ **Removida toda lógica de cálculo**
- ✅ MTM agora é **pré-calculado** e disponível na base MySQL
- ✅ Nova estrutura:
  - `codigo_operacao` (int)
  - `valor_mtm` (decimal)
  - `valor_accrual` (decimal)
  - `sequencial_perna` (int)

#### Cotações
- ✅ **Removida implementação de processamento**
- ✅ Cotações agora são **pré-inseridas** na base MySQL
- ✅ Nova estrutura simplificada:
  - `codigo_cotacao` (int)
  - `ticker_ativo` (string)
  - `fonte` (string)
  - `data` (date)
  - `preco_fechamento` (decimal)

---

## 🎯 Cenário de Teste: Autocall Worst-Off

### Conceito Worst-Off

No **Worst-Off**, o retorno do COE é baseado no ativo de **pior desempenho** da cesta. Para o autocall ser ativado:

1. ✅ **Todos os ativos** devem atingir suas barreiras individuais
2. ✅ O sistema identifica qual ativo teve a **menor variação** (worst-off)
3. ✅ A liquidação é baseada no desempenho desse ativo

### Estrutura do COE de Exemplo

**COE-WORSTOFF-001** - COE Tech Worst-Off com Autocall

**Cesta de Ativos:**
- 🔵 META (25%) - Cotação Inicial: $450.00
- 🔵 SNOW - Snowflake (25%) - Cotação Inicial: $200.00
- 🔵 OPENAI (25%) - Cotação Inicial: $500.00
- 🔵 MSFT - Microsoft (25%) - Cotação Inicial: $380.00

**Barreiras:**
- Todas as ações devem atingir ou superar a cotação inicial (condição UP)
- Data de Observação: 21/10/2025

**Cotações em 21/10/2025:**
- META: $465.00 → +3.33% ⚠️ **WORST-OFF**
- SNOW: $220.00 → +10.00%
- OPENAI: $550.00 → +10.00%
- MSFT: $418.00 → +10.00%

---

## 📋 Fluxo de Teste Completo - Autocall Worst-Off

### Passo 1: Publicar Cotações Diárias

Simule a chegada das cotações do dia 21/10/2025:

#### Cotação META
```http
POST /api/eventos/cotacao
Content-Type: application/json

{
  "tickerAtivo": "META",
  "precoFechamento": 465.00,
  "data": "2025-10-21"
}
```

#### Cotação SNOW
```http
POST /api/eventos/cotacao
Content-Type: application/json

{
  "tickerAtivo": "SNOW",
  "precoFechamento": 220.00,
  "data": "2025-10-21"
}
```

#### Cotação OPENAI
```http
POST /api/eventos/cotacao
Content-Type: application/json

{
  "tickerAtivo": "OPENAI",
  "precoFechamento": 550.00,
  "data": "2025-10-21"
}
```

#### Cotação MSFT
```http
POST /api/eventos/cotacao
Content-Type: application/json

{
  "tickerAtivo": "MSFT",
  "precoFechamento": 418.00,
  "data": "2025-10-21"
}
```

**O que acontece após cada cotação:**
1. ✅ Evento de cotação é publicado no tópico `topico.cotacoes`
2. ✅ **ObservadorCotacao** recebe o evento
3. ✅ Sistema busca barreiras ativas para o ticker
4. ✅ Verifica se a barreira foi atingida (preço >= nível_barreira)
5. ✅ Se atingida, publica evento no tópico `topico.barreiras`

---

### Passo 2: Processamento Automático das Barreiras

Quando **todas as 4 barreiras** forem atingidas:

**O que acontece:**
1. ✅ **ObservadorBarreira** recebe cada evento de barreira atingida
2. ✅ Marca a barreira como atingida no banco
3. ✅ Verifica se todas as barreiras do COE foram atingidas (lógica Worst-Off)
4. ✅ Calcula a variação de cada ativo
5. ✅ Identifica o **ativo com menor variação** (META = +3.33%)
6. ✅ **Inicia automaticamente a Saga de Autocall**

**Log esperado:**
```
🎯 AUTOCALL ATIVADO! Todas as barreiras do COE foram atingidas (Worst-Off)
Ativo META: Cotação Inicial=450.00, Cotação Dia=465.00, Variação=3.33%
Ativo SNOW: Cotação Inicial=200.00, Cotação Dia=220.00, Variação=10.00%
Ativo OPENAI: Cotação Inicial=500.00, Cotação Dia=550.00, Variação=10.00%
Ativo MSFT: Cotação Inicial=380.00, Cotação Dia=418.00, Variação=10.00%
Ativo Worst-Off: META com variação de 3.33%
Saga de Autocall iniciada para COE
```

---

### Passo 3: Execução da Saga de Autocall

A saga é iniciada automaticamente com 6 etapas:

#### Etapa 1: Verificar Barreira
- ✅ Valida que todas as barreiras foram atingidas

#### Etapa 2: Processar Atingimento
- ✅ Registra o atingimento no histórico

#### Etapa 3: Iniciar Liquidação
- ✅ Cria registro de liquidação
- ✅ Data de liquidação: D+2 (23/10/2025)

#### Etapa 4: Calcular Valor Liquidação
- ✅ Calcula valor baseado no worst-off (META com +3.33%)
- ✅ Aplica rentabilidade sobre o valor nominal

#### Etapa 5: Liquidar Posições
- ✅ Atualiza posições dos clientes
- ✅ Calcula valor de resgate para cada cliente

#### Etapa 6: Encerrar COE
- ✅ Marca COE como inativo
- ✅ Finaliza todas as barreiras

---

### Passo 4: Acompanhar a Saga

```http
GET /api/saga
```

Retorna todas as sagas, incluindo a saga de Autocall com:
- Estado: `Concluida`
- Tipo: `AtivacaoAutocall`
- 6 etapas executadas com sucesso
- Dados de contexto com informações do worst-off

```http
GET /api/saga/{sagaId}
```

Retorna detalhes completos da saga específica.

---

## 🔍 Consultando Resultados

### Verificar Barreiras Atingidas
```http
GET /api/coe/{coeId}/barreiras
```

Deve mostrar todas as 4 barreiras marcadas como `atingida: true`.

### Verificar Liquidação Criada
```http
GET /api/coe/{coeId}/liquidacoes
```

Deve mostrar:
- Tipo: `AUTOCALL`
- Data: 23/10/2025
- Status: `Processada`
- Percentual de retorno baseado no worst-off

### Verificar Posições dos Clientes
```http
GET /api/coe/{coeId}/posicoes
```

Posições devem estar atualizadas com valores de liquidação.

---

## 🎭 Observer Pattern - Fluxo Completo

### 1. Observação de Cotações Diárias

```
Cotação Publicada (topico.cotacoes)
         ↓
ObservadorCotacao escuta
         ↓
Para cada ativo do COE:
  - Busca barreiras ativas
  - Verifica condição (UP/DOWN)
  - Se atingida → Publica evento de barreira
```

### 2. Observação de Barreiras (1:N para COE)

```
Barreira Atingida (topico.barreiras)
         ↓
ObservadorBarreira escuta
         ↓
Marca barreira como atingida
         ↓
Verifica se todas as barreiras do COE foram atingidas
         ↓
Se SIM (Worst-Off completo):
  - Calcula variação de cada ativo
  - Identifica worst-off
  - Inicia Saga de Autocall
```

### 3. Autocall - Agregação de Barreiras

**Lógica Worst-Off:**
- Sistema aguarda **TODAS** as barreiras serem atingidas
- Somente quando a última barreira é atingida, o autocall é ativado
- O retorno é baseado no ativo de **pior desempenho**

---

## 🧪 Testes Adicionais

### Cenário: Barreira Não Atingida

Se uma das ações **não** atingir a barreira:

```http
POST /api/eventos/cotacao
{
  "tickerAtivo": "META",
  "precoFechamento": 440.00,  // Abaixo da barreira de 450.00
  "data": "2025-10-21"
}
```

**Resultado:**
- ❌ Barreira META não é atingida
- ❌ Autocall **não** é ativado
- ✅ Sistema continua observando

### Cenário: Teste de Compensação

Forçar rollback da saga:

```http
POST /api/saga/{sagaId}/compensar
```

**O que acontece:**
1. ✅ COE volta para estado ativo
2. ✅ Liquidação é removida
3. ✅ Barreiras voltam para não atingidas
4. ✅ Histórico de compensação é registrado

---

## 📈 Dados Pré-Inseridos

O script `init.sql` já cria:
- ✅ 1 COE (COE-WORSTOFF-001)
- ✅ 4 Ativos (META, SNOW, OPENAI, MSFT)
- ✅ 4 Barreiras (uma por ativo)
- ✅ 8 Cotações (inicial + dia de observação)
- ✅ 2 Posições de clientes
- ✅ 2 Registros de MTM
- ✅ Observadores inscritos nos tópicos

**Você só precisa publicar os eventos de cotação para ativar o fluxo!**

---

## 🚨 Troubleshooting

### Autocall não foi ativado
- Verifique se **todas** as 4 cotações foram publicadas
- Confirme que todas as barreiras estão com `atingida: true`
- Verifique os logs do ObservadorBarreira

### Saga não iniciou
- Verifique se o tópico `topico.barreiras` tem observadores inscritos
- Confirme que o ObservadorBarreira está registrado no DI container

### Cotações não estão processando barreiras
- Verifique se o tópico `topico.cotacoes` tem observadores inscritos
- Confirme que as cotações estão sendo publicadas no tópico correto
- Verifique a data de observação das barreiras

---

## 📚 Referências

- **Padrão Saga:** Orquestração de transações distribuídas
- **Padrão Observer:** Notificação de eventos assíncronos
- **Worst-Off:** Estrutura de COE baseada no pior desempenho
- **Autocall:** Liquidação antecipada por atingimento de barreira
