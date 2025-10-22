# QUICKSTART – Saga de Autocall (Worst-Of)

Este guia mostra, de ponta a ponta, como rodar a saga de Autocall para uma operação do tipo Worst-Of com uma cesta de ativos: META, SNOW, MSFT e OPENAI.

Observações importantes:
- MTM não é calculado pela aplicação (já está disponível na base – tabela `mtm`).
- Cotações podem ser pré-inseridas via `init.sql` e/ou publicadas via endpoint apenas para simular chegada de eventos.
- Toda a orquestração ocorre via padrão Saga + Observer, com tópicos: `topico.cotacao` → `topico.barreira` → `topico.autocall`.

---

## 1) Pré‑requisitos

- .NET 8.0
- MySQL 8.0

Configuração de conexão (arquivo `OrquestradorSagaCarteira/appsettings.json`):
- Banco padrão: `saga_autocall`
- Connection string: ajuste usuário/senha conforme seu ambiente

---

## 2) Preparar o banco

- Execute o arquivo `init.sql` no MySQL (ele cria o schema, views, procedures e popula dados de exemplo: operações, ativos, barreiras e cotações iniciais/seguinte).
- Ao final, o script executa:
  - `CALL sp_estatisticas_sistema();`
  - SELECTs de conferência (operações e cotações)

Se preferir, você pode complementar cotações de teste direto na tabela `cotacao` (pré-inseridas) ou usar o endpoint de eventos (seção 5) para simular publicação diária.

---

## 3) Build e execução da API

```bash
cd OrquestradorSagaCarteira
 dotnet restore
 dotnet run
```

- A API sobe com Swagger (dev): `https://localhost:<porta>/swagger`

---

## 4) Inicializar os Observers

Os Observers são contínuos e precisam estar inscritos nos tópicos.

- Endpoint: `POST /api/sagas`
- Efeito: inscreve
  - Observador de Barreira em `topico.cotacao`
  - Observador de Autocall em `topico.barreira`
  - Observador de Desfazimento (liquidação) em `topico.autocall`

Resposta esperada (resumo): nomes dos observadores e seus tópicos.

---

## 5) Criar uma Operação (Worst‑Of)

- Endpoint: `POST /api/sagas/operacoes`
- Exemplo de payload (Worst‑Of com META, SNOW, MSFT, OPENAI e barreira de Autocall na cesta):

```json
{
  "codigoOperacao": "COE-TECH-2025",
  "descricao": "COE Tech Worst-Of (META, SNOW, MSFT, OPENAI)",
  "dataVencimento": "2026-10-21",
  "valorNominal": 100000.00,
  "tipoEstrutura": "WorstOf",
  "ativos": [
    { "ticker": "META",   "cotacaoInicial": 450.00,   "percentualParticipacao": 25.0 },
    { "ticker": "SNOW",   "cotacaoInicial": 180.00,   "percentualParticipacao": 25.0 },
    { "ticker": "MSFT",   "cotacaoInicial": 380.00,   "percentualParticipacao": 25.0 },
    { "ticker": "OPENAI", "cotacaoInicial": 100.00,   "percentualParticipacao": 25.0 }
  ],
  "barreiras": [
    { "ticker": "META",   "tipoBarreira": "AutoCall", "condicao": "UP", "nivelBarreira": 0.00,  "dataObservacao": "2025-10-21" },
    { "ticker": "SNOW",   "tipoBarreira": "AutoCall", "condicao": "UP", "nivelBarreira": 0.00,  "dataObservacao": "2025-10-21" },
    { "ticker": "MSFT",   "tipoBarreira": "AutoCall", "condicao": "UP", "nivelBarreira": 0.00,  "dataObservacao": "2025-10-21" },
    { "ticker": "OPENAI", "tipoBarreira": "AutoCall", "condicao": "UP", "nivelBarreira": 0.00,  "dataObservacao": "2025-10-21" }
  ]
}
```

Observações:
- `tipoEstrutura`: "WorstOf" → usa o ativo de pior desempenho para a decisão de Autocall.
- Barreiras individuais (`KnockIn`) com `condicao: "UP"` significam: barreira é atingida quando cotação atual ≥ cotação inicial.
- A barreira de Autocall é de CESTA (ticker nulo) e é observada pelo Observer de Autocall após as individuais atingirem.

Guarde o `id` retornado para consultar a operação depois.

---

## 6) Publicar cotações do dia (simulação)

Você pode usar cotações já pré-inseridas no banco (via `init.sql`) ou publicar via API para simular a chegada de mercado.

- Endpoint: `POST /api/eventos/cotacoes`
- Exemplo (todas acima da cotação inicial; Worst-Of atinge quando todas atingem):

```json
{ "ticker": "META",   "data": "2025-10-21", "precoFechamento": 465.00, "fonte": "B3" }
```
```json
{ "ticker": "SNOW",   "data": "2025-10-21", "precoFechamento": 190.00, "fonte": "B3" }
```
```json
{ "ticker": "MSFT",   "data": "2025-10-21", "precoFechamento": 395.00, "fonte": "B3" }
```
```json
{ "ticker": "OPENAI", "data": "2025-10-21", "precoFechamento": 105.00, "fonte": "INTERNAL" }
```

O que acontece:
1) O endpoint persiste a cotação (opcional para seu cenário se já estiver pré-carregada) e publica no `topico.cotacao`.
2) O Observador de Barreira verifica a barreira individual do ativo (UP) → persiste evento → publica `topico.barreira`.
3) O Observador de Autocall agrega a cesta (Worst‑Of) → se todas as individuais estiverem atingidas e a variação mínima da cesta ≥ nível de autocall, persiste autocall e publica `topico.autocall`.
4) O Observador de Desfazimento agenda a liquidação (D+2 por padrão no exemplo de código).

---

## 7) Acompanhar as Sagas

- Listar sagas: `GET /api/sagas`
- Detalhar uma saga específica: `GET /api/sagas/{id}`

Campos úteis:
- `EstadoSaga`: Iniciada, EmExecucao, Concluida, Compensando, Compensada
- `Etapas`: ordem, estado da etapa, ação executada e mensagens/erros

---

## 8) Conferir a Operação e Liquidação

- Operação: `GET /api/sagas/operacoes/{id}`
  - Verifique `Ativa = false` após agendamento de liquidação (no fluxo de exemplo)

Consulta direta (opcional) no MySQL:
- Liquidações agendadas:
  ```sql
  SELECT * FROM liquidacao_agendada ORDER BY data_liquidacao;
  ```
- Eventos de barreira:
  ```sql
  SELECT * FROM evento_barreira ORDER BY data_evento DESC;
  ```
- Eventos de autocall:
  ```sql
  SELECT * FROM evento_autocall ORDER BY data_evento DESC;
  ```

---

## 9) Cenário de referência – Worst‑Of com META como pior desempenho

Para validar o caso solicitado (Worst‑Of com META como o ativo de menor taxa de variação, ainda acima do inicial):
- Configure as cotações do dia de forma que todas estejam ≥ cotação inicial.
- Siga a ordem de publicação (META, SNOW, MSFT, OPENAI) ou publique em lote.
- O Observador de Autocall verificará que, no Worst‑Of, todas as barreiras individuais foram atingidas e que a variação mínima (pior ativo – aqui, META) ainda é ≥ 0% (ou ≥ nível configurado). Se a barreira de Autocall estiver, por exemplo, em 5%, ajuste as cotações para que a variação mínima seja ≥ 5%.
- Sendo atingido, a liquidação será agendada para a data definida pelo fluxo (ex.: D+2).

---

## 10) Troubleshooting

- "Não disparou Autocall":
  - Verifique se TODAS as barreiras individuais estão `atingida = true` (Worst‑Of exige todas atingidas)
  - Confirme o nível da barreira de Autocall (ex.: 5%) e a variação mínima da cesta
- "Observers não reagem":
  - Garanta que você chamou `POST /api/sagas` para inscrever os Observers
  - Veja logs da aplicação (startup e processamento de eventos)
- "Dados não aparecem":
  - Confira o schema `saga_autocall` e se o `init.sql` rodou sem erros
  - Verifique timestamps e datas dos eventos/cotações

---

## 11) Resumo do Fluxo

```
/api/sagas                  → inscreve Observers
/api/sagas/operacoes        → cria operação (Worst‑Of) com ativos e barreiras
/api/eventos/cotacoes       → publica cotação do dia (1 por ativo)
                             ↳ topico.cotacao → VerificarBarreira → topico.barreira
                             ↳ topico.barreira → Autocall (Worst‑Of) → topico.autocall
                             ↳ topico.autocall → agendar liquidação (D+2)
GET /api/sagas              → acompanhar execução
GET /api/sagas/{id}         → detalhes da saga
GET /api/sagas/operacoes/{id} → verificar estado da operação
```

Dica: use o Swagger para disparar as requisições e acompanhar o payload/retornos.

