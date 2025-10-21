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

## 📋 Fluxo de Teste Completo

### Cenário 1: Processar Valorização MTM Diária

#### Passo 1: Processar Cotações dos Ativos
```http
POST /api/saga/processar-cotacao
{
  "codigoAtivo": "PETR4",
  "tipoAtivo": "ACAO",
  "dataReferencia": "2025-10-21",
  "precoFechamento": 38.50
}
```

#### Passo 2: Publicar Evento de MTM (Simula Consumer)
```http
POST /api/eventos/mtm
{
  "coeId": "guid-do-coe",
  "dataReferencia": "2025-10-21"
}
```

**O que acontece:**
1. ✅ Evento MTM é publicado no tópico `topico.mtm`
2. ✅ ObservadorMtm recebe o evento
3. ✅ Saga de Valorização MTM é iniciada automaticamente
4. ✅ 5 etapas são executadas sequencialmente:
   - Calcular MTM Renda Fixa
   - Calcular MTM Renda Variável
   - Consolidar MTM
   - Calcular Valorização Contábil
   - Atualizar Posição Cliente

#### Passo 3: Acompanhar a Saga
```http
GET /api/saga
```

Você verá a saga com suas etapas e estados.

---

### Cenário 2: Processar Evento Corporativo (Split)

#### Passo 1: Criar Evento Corporativo no Banco
(Use o script SQL ou crie via API se tiver endpoint)

#### Passo 2: Publicar Evento Corporativo
```http
POST /api/eventos/evento-corporativo
{
  "eventoId": "guid-do-evento",
  "tipoEvento": "Split",
  "codigoAtivo": "PETR4",
  "fatorAjuste": 2.0
}
```

**O que acontece:**
1. ✅ Evento é publicado no tópico `topico.eventos-corporativos`
2. ✅ ObservadorEventoCorporativo recebe o evento
3. ✅ Saga de Evento Corporativo é iniciada
4. ✅ 3 etapas são executadas:
   - Processar Split (dobra quantidade, divide preço)
   - Ajustar Posições
   - Realizar Ajuste Contábil

---

### Cenário 3: Processar Autocall

#### Passo 1: Publicar Evento de Barreira
```http
POST /api/eventos/barreira
{
  "barreiraId": "guid-da-barreira",
  "coeId": "guid-do-coe",
  "tipoBarreira": "Autocall",
  "dataObservacao": "2025-10-21"
}
```

**O que acontece:**
1. ✅ Evento é publicado no tópico `topico.barreiras`
2. ✅ ObservadorBarreira recebe o evento
3. ✅ Saga de Autocall é iniciada
4. ✅ 6 etapas são executadas:
   - Verificar Barreira
   - Processar Atingimento
   - Iniciar Liquidação
   - Calcular Valor Liquidação
   - Liquidar Posições
   - Encerrar COE

---

## 🔍 Monitoramento e Debug

### Consultar Estado de uma Saga
```http
GET /api/saga/{sagaId}
```

Retorna:
- Estado da saga (Iniciada, EmExecucao, Concluida, Compensando, etc)
- Lista de etapas com seus estados
- Dados de entrada/saída de cada etapa
- Histórico de compensações

### Listar Todas as Sagas
```http
GET /api/saga?pagina=1&tamanhoPagina=50
```

### Forçar Compensação (Teste de Rollback)
```http
POST /api/saga/{sagaId}/compensar
```

**O que acontece:**
1. ✅ Saga entra no estado `Compensando`
2. ✅ Etapas são compensadas na ordem inversa
3. ✅ Cada compensação é registrada no `historico_compensacao`
4. ✅ Saga finaliza no estado `Compensada`

---

## 📊 Consultando Dados de Negócio

### Listar COEs
```http
GET /api/coe
```

### Obter Detalhes de um COE
```http
GET /api/coe/{coeId}
```

### Ver Histórico de MTM
```http
GET /api/coe/{coeId}/mtms
```

### Ver Posições de Clientes
```http
GET /api/coe/{coeId}/posicoes
```

### Ver Barreiras
```http
GET /api/coe/{coeId}/barreiras
```

---

## 🎯 Testando Compensações

### Teste 1: Simular Falha na Consolidação MTM

1. Modifique temporariamente a classe `AcaoConsolidarMtm` para lançar uma exceção
2. Inicie uma saga de valorização MTM
3. Observe as 2 primeiras etapas serem executadas
4. A 3ª etapa falhará
5. As etapas 1 e 2 serão compensadas automaticamente

### Teste 2: Compensação Manual

1. Execute uma saga completa
2. Force a compensação via endpoint
3. Verifique que os dados foram revertidos

---

## 📝 Logs

Os logs mostram:
- ✅ Início e fim de cada saga
- ✅ Execução de cada etapa
- ✅ Eventos publicados e recebidos
- ✅ Observadores notificados
- ✅ Compensações executadas
- ✅ Erros e exceções

Exemplo de log:
```
info: OrquestradorSagaCarteira.Aplicacao.Servicos.PublicadorEventos[0]
      Evento MtmCalculado publicado no tópico topico.mtm
      
info: OrquestradorSagaCarteira.Aplicacao.Observadores.ObservadorMtm[0]
      Observador de MTM recebeu evento do tópico topico.mtm
      
info: OrquestradorSagaCarteira.Aplicacao.Servicos.OrquestradorSaga[0]
      Saga {SagaId} do tipo ValorizacaoMtm iniciada com 5 etapas
      
info: OrquestradorSagaCarteira.Aplicacao.Servicos.OrquestradorSaga[0]
      Etapa {EtapaId} da Saga {SagaId} concluída com sucesso
```

---

## 🔧 Troubleshooting

### Erro: "Connection refused" no MySQL
- Verifique se o MySQL está rodando
- Ajuste a porta na connection string
- Confirme usuário e senha

### Erro: "Table doesn't exist"
- Execute o script `init.sql`
- Verifique se o database foi criado

### Saga não inicia automaticamente
- Verifique se os observadores foram registrados no `Program.cs`
- Confirme que o evento foi publicado no tópico correto
- Verifique os logs para mensagens de erro

### Compensação não reverte dados
- Verifique a implementação do método `CompensarAsync` na ação
- Confirme que os dados de saída foram salvos corretamente
- Consulte a tabela `historico_compensacao`

---

## 🎓 Próximos Passos

1. **Adicionar autenticação/autorização** nos endpoints
2. **Implementar retry automático** para etapas que falharam
3. **Criar dashboard** para visualização das sagas
4. **Adicionar notificações** para eventos importantes
5. **Implementar cache** para consultas frequentes
6. **Adicionar métricas** (tempo de execução, taxa de sucesso)
7. **Criar testes unitários e de integração**
8. **Implementar circuit breaker** para serviços externos

