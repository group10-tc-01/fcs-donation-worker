# fcs-donation-worker

Worker de processamento assincrono de doacoes da plataforma Conexao Solidaria. Consome eventos `DonationReceivedEvent` publicados no Kafka pela `fcs-donations`, atualiza o status da doacao no `DonationsDb` e notifica a `fcs-campaign` por API interna para refletir o valor arrecadado.

> Microsservico que compoe o MVP da Conexao Solidaria junto a `fcs-identity`, `fcs-campaign`, `fcs-donations`, `fcs-audit-logs`, `fcs-web` e `fcs-infra`.

---

## Responsabilidades

- Consumir eventos `DonationReceivedEvent` do topico Kafka `donation-received`.
- Garantir idempotencia por `eventId + topic` usando `ProcessedMessages`.
- Ler e atualizar doacoes no `DonationsDb`.
- Atualizar a doacao para `Processed` ou `Failed`.
- Preencher `FailureReason` quando a doacao falhar.
- Chamar a API interna da `fcs-campaign` para registrar doacao processada.
- Publicar eventos explicitos de auditoria quando houver processamento, falha ou duplicidade.
- Expor apenas endpoints operacionais `/health` e `/metrics` quando configurados no ambiente de execucao.

O `fcs-donation-worker` nao possui endpoints HTTP de negocio e nao escreve diretamente no banco da `fcs-campaign`.

Documentacao completa da arquitetura: [group10-tc-01/fcs-fase05-docs](https://github.com/group10-tc-01/fcs-fase05-docs).

Referencias diretas:

- [Visao geral da arquitetura](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/architecture/overview.md)
- [Fluxo da fcs-donations](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/architecture/fcs-donations-flow.md)
- [Modelo de banco de dados](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/architecture/database-model.md)
- [Endpoints consolidados](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/architecture/endpoints.md)
- [Fluxos dos endpoints e workers](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/architecture/endpoint-flows.md)

ADRs relevantes:

- [ADR 0008 - Kafka para eventos de doacao](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0008-use-kafka-for-donation-events.md)
- [ADR 0009 - Worker atualiza status da doacao](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0009-worker-updates-donation-status.md)
- [ADR 0010 - Worker atualiza campanhas por API interna](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0010-worker-updates-campaigns-through-internal-api.md)
- [ADR 0018 - Kafka dentro do Kubernetes](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0018-run-kafka-inside-kubernetes.md)
- [ADR 0022 - Reuso do fcs-pipelines](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0022-reuse-fcs-pipelines-for-ci-cd.md)
- [ADR 0023 - Estrutura interna .NET da fase 04](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0023-use-phase-04-dotnet-service-structure.md)

---

## Fluxo de Processamento

```text
fcs-donations
        |
        | Kafka topic donation-received
        v
fcs-donation-worker
        |
        +--> DonationsDb: status Processed ou Failed
        |
        +--> fcs-campaign: POST /internal/campaigns/{id}/donation-processed
        |
        +--> Kafka topic audit-log-requested
```

Regras importantes:

- A `fcs-donations` publica `DonationReceivedEvent` via outbox.
- O worker registra mensagens processadas para ignorar redeliveries.
- Duplicidade deve ser tratada como sucesso operacional e nao deve chamar campanhas novamente.
- A atualizacao da campanha sempre passa pela API interna da `fcs-campaign`.
- O offset Kafka so deve ser confirmado apos processamento de negocio bem-sucedido, duplicidade idempotente ou falha controlada registrada.

---

## Contrato Kafka

Topico:

```text
donation-received
```

Evento:

```text
DonationReceivedEvent
```

Payload minimo:

```json
{
  "eventId": "3c03f6e3-7c8d-43b8-8f94-4c4ef3b6b4e6",
  "donationId": "11111111-1111-1111-1111-111111111111",
  "campaignId": "22222222-2222-2222-2222-222222222222",
  "donorId": "33333333-3333-3333-3333-333333333333",
  "amount": 100.00,
  "occurredAt": "2026-05-18T20:00:00Z"
}
```

Campos obrigatorios:

| Campo | Descricao |
|-------|-----------|
| `eventId` | Identificador unico usado para idempotencia |
| `donationId` | Doacao criada pela `fcs-donations` |
| `campaignId` | Campanha que recebera a doacao |
| `donorId` | Perfil do doador sem foreign key para `IdentityDb` |
| `amount` | Valor da doacao |
| `occurredAt` | Data/hora UTC do evento original |

---

## Integracao Interna

Chamada esperada para campanhas:

```text
POST /internal/campaigns/{id}/donation-processed
```

Payload:

```json
{
  "donationId": "11111111-1111-1111-1111-111111111111",
  "amount": 100.00,
  "processedAt": "2026-05-18T20:00:00Z"
}
```

---

## Estrutura do Projeto

```text
src/
  Fcs.Donation.Worker.Application/    # Consumers Kafka, idempotencia e servicos de processamento
    Common/
    Features/
  Fcs.Donation.Worker.Worker/         # Host .NET Worker, configuracao e Dockerfile
tests/
  Fcs.Donation.Worker.UnitTests/
```

Estrutura interna alinhada ao padrao da fase 04 ([ADR 0023](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0023-use-phase-04-dotnet-service-structure.md)).

---

## Persistencia

- Engine: SQL Server.
- Database: `DonationsDb`.
- Tabelas usadas pelo worker:
  - `Donations`
  - `ProcessedMessages`

O worker nao cria foreign keys para bancos de outros servicos.

Eventos de auditoria esperados:

- `DonationProcessed`
- `DonationFailed`
- `DuplicateMessageIgnored`

---

## Pre-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) e Docker Compose
- Portas livres no host: `1433` (SQL Server), `9092` (Kafka), `8081` (Kafka UI), `5341` (Seq).

---

## Subindo o Ambiente Local

O `docker-compose.yml` deste repositorio sobe apenas as dependencias deste worker (SQL Server, Kafka, Kafka UI e Seq) e, opcionalmente, o proprio worker. Para o ambiente completo integrado da Conexao Solidaria utilize o repositorio `fcs-infra`.

### 1. Subir dependencias

```bash
docker compose up -d sqlserver zookeeper kafka kafka-ui seq
```

URLs uteis:

- Kafka UI: http://localhost:8081
- Seq: http://localhost:5341
- SQL Server: `Server=localhost,1433;Database=DonationsDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True`

### 2. Rodar o worker localmente

```bash
dotnet restore
dotnet build
dotnet run --project src/Fcs.Donation.Worker.Worker
```

### 2b. Rodar o worker tambem em container

```bash
docker compose up -d --build fcs-donation-worker
```

### 3. Publicar evento manual para teste

```bash
docker exec -i kafka-fcs-donation-worker kafka-console-producer \
  --bootstrap-server kafka:29092 \
  --topic donation-received
```

Payload de exemplo:

```json
{"eventId":"11111111-1111-1111-1111-111111111111","donationId":"22222222-2222-2222-2222-222222222222","campaignId":"33333333-3333-3333-3333-333333333333","donorId":"44444444-4444-4444-4444-444444444444","amount":100.00,"occurredAt":"2026-05-18T20:00:00Z"}
```

---

## Testes

```bash
# Todos os testes
dotnet test

# Projeto de testes unitarios
dotnet test tests/Fcs.Donation.Worker.UnitTests
```

Cobertura minima exigida pela esteira: **80%** ([ADR 0025](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0025-test-strategy-for-apis-and-worker.md)).

---

## Observabilidade

- Logs estruturados com **Serilog** enviados ao **Seq** em ambiente local.
- Consumo Kafka com logs de duplicidade, falha de processamento e retry por nao commit de offset.
- Endpoints operacionais esperados no ambiente de execucao:
  - `/health`
  - `/metrics`

Esses endpoints nao sao publicados no Azure API Management ([ADR 0027](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0027-keep-internal-apis-cluster-private.md)).

---

## CI/CD

A esteira fica em `.github/workflows/` reutilizando os workflows reutilizaveis do repositorio `fcs-pipelines` ([ADR 0022](https://github.com/group10-tc-01/fcs-fase05-docs/blob/main/adr/0022-reuse-fcs-pipelines-for-ci-cd.md)):

- `branch-name-check.yml` - politica de nomes de branch
- `dotnet-service-ci.yml` - build .NET, testes, SonarCloud, Trivy e validacoes do servico

Gates principais: secret scan (Gitleaks), dependency scan, restore/build, testes com cobertura minima de 80%, SonarCloud opcional e regras de protecao por branch.

---

## Kubernetes

Manifests Kubernetes deste worker (Deployment, Service, ConfigMap, Secret) ficam no repositorio `fcs-infra`, junto ao ambiente integrado Kind/AKS.

Namespace alvo: `fcs-donation-worker`.

---

## Como este Worker Atende ao Hackathon

| Requisito do hackathon | Onde e atendido |
|------------------------|-----------------|
| Microsservico distinto | `fcs-donation-worker` separado das APIs de negocio |
| Mensageria assincrona | Consumo do topico Kafka `donation-received` |
| Processamento em background | Worker .NET para fechar o fluxo de doacoes |
| Observabilidade | Logs estruturados, `/health` e `/metrics` |
| Imagem Docker e pipeline | `Dockerfile`, `docker-compose.yml` e workflows em `.github/workflows` |

O `fcs-donation-worker` fecha o fluxo assincrono entre intencao de doacao, processamento, atualizacao de status e reflexo do valor arrecadado na campanha.
