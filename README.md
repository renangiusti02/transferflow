# TransferFlow

TransferFlow is a backend-focused wallet transfer API built with **.NET 10, ASP.NET Core, Entity Framework Core, and PostgreSQL**.

The project is designed as a compact engineering playground for building and demonstrating backend concepts that matter in financial and distributed systems: **atomic transactions, idempotency, concurrency control, reliable messaging, observability, and automated testing**.

> TransferFlow is a portfolio and learning project. It is not intended to be a production banking or payment platform.

## Current Features

* Create and retrieve wallets
* Transfer balance atomically between wallets
* PostgreSQL persistence with Entity Framework Core
* HTTP idempotency through `Idempotency-Key`
* Payload validation when an idempotency key is reused
* Optimistic wallet concurrency using PostgreSQL `xmin`
* Whole-operation retry after concurrency conflicts
* Transactional Outbox for reliable event publication
* Amazon SQS integration with LocalStack for local development
* At-least-once message delivery with idempotent SQS message processing
* Retry through SQS redelivery and dead-letter queue handling
* Structured logging with correlation IDs propagated across HTTP, Outbox and SQS
* Liveness and readiness health checks
* Domain, Application and PostgreSQL integration tests
* Deterministic concurrency tests using independent `DbContext` instances
* GitHub Actions CI with a disposable PostgreSQL service
* OpenAPI support

## Why TransferFlow?

A transfer API looks simple:

```text
Wallet A
   |
   | 30.00
   v
Wallet B
```

But a reliable implementation raises several backend engineering questions:

* What happens if debit succeeds but credit fails?
* What if the client retries a request after a timeout?
* What if two identical requests arrive at the same time?
* What if two different transfers compete for the same balance?
* How do we publish events without losing them after a database commit?
* How do we make consumers safe against duplicate messages?

TransferFlow evolves incrementally around these problems instead of introducing infrastructure before it is justified.

## Architecture

TransferFlow keeps the synchronous business transaction separate from asynchronous message delivery.

```text
                         HTTP
                          |
                          v
                  TransferFlow.Api
                          |
                          v
               TransferFlow.Application
                    /            \
                   v              v
        TransferFlow.Domain    Contracts
                                  ^
                                  |
                    TransferFlow.Infrastructure
                                  |
                    +-------------+-------------+
                    |                           |
                    v                           v
                PostgreSQL                  Amazon SQS
                    |                           ^
                    |                           |
                    +--> Outbox Messages -------+
                                                |
                                                v
                                      SQS Consumer Worker
                                                |
                                                v
                                      Processed Messages
                                                |
                                                v
                                           PostgreSQL
```

### Synchronous transfer flow

```text
POST /transfers
      |
      v
CreateTransferUseCase
      |
      +--> debit source wallet
      |
      +--> credit destination wallet
      |
      +--> persist Transfer
      |
      +--> persist OutboxMessage
      |
      v
SaveChangesAsync()
      |
      v
single PostgreSQL transaction
```

The transfer, wallet balance changes and outbox event are committed atomically.

The API does not publish directly to SQS as part of the HTTP transaction.

### Asynchronous event flow

```text
PostgreSQL Outbox
      |
      v
OutboxProcessor
      |
      v
Amazon SQS
      |
      v
SqsConsumerBackgroundService
      |
      v
SqsMessageProcessor
      |
      v
processed_messages
```

Outbox publication and message consumption use at-least-once delivery semantics.

Consumers therefore cannot assume that a message will be delivered only once.

A logical `message-id` is persisted in `processed_messages` so repeated deliveries can be handled idempotently.

### Dependency direction

```text
Domain
  └── no project dependencies

Application
  └── Domain

Infrastructure
  ├── Application
  └── Domain

Api
  ├── Application
  └── Infrastructure
```

Api acts as the composition root and owns HTTP-specific concerns such as middleware, health checks and dependency injection.

The Domain layer contains business rules without infrastructure dependencies.

Application orchestrates use cases and defines contracts required by infrastructure.

Infrastructure implements persistence, messaging and background processing.

## Project Structure

```text
TransferFlow/
├── src/
│   ├── TransferFlow.Api/
│   ├── TransferFlow.Application/
│   ├── TransferFlow.Domain/
│   └── TransferFlow.Infrastructure/
│
├── tests/
│   ├── TransferFlow.Domain.Tests/
│   ├── TransferFlow.Application.Tests/
│   └── TransferFlow.Integration.Tests/
│
├── compose.yaml
├── .env.example
├── .editorconfig
├── README.md
└── TransferFlow.slnx
```

## Tech Stack

* **.NET 10**
* **ASP.NET Core**
* **C#**
* **Entity Framework Core**
* **Npgsql**
* **PostgreSQL 18**
* **Amazon SQS**
* **LocalStack**
* **Docker Compose**
* **xUnit**
* **GitHub Actions**

## Domain

### Wallet

A wallet has an identity and a balance.

Current invariants include:

* credits must be positive
* debits must be positive
* amounts support at most two decimal places
* a wallet cannot be debited beyond its available balance

### Transfer

A transfer records:

* source wallet
* destination wallet
* amount
* creation timestamp
* idempotency key

Current invariants include:

* source and destination must be valid wallet identifiers
* source and destination must be different
* amount must be positive
* amount supports at most two decimal places
* an idempotency key is required and limited in length

## Atomic Transfers

A successful transfer modifies three pieces of state:

```text
Source Wallet       Modified
Destination Wallet  Modified
Transfer             Added
        |
        v
SaveChangesAsync()
        |
        v
PostgreSQL Transaction
```

All changes are persisted through a single unit of work.

If persistence fails, the database transaction rolls the operation back rather than leaving only part of the transfer persisted.

## Idempotency

`POST /transfers` requires an `Idempotency-Key` header.

Example:

```http
POST /transfers
Content-Type: application/json
Idempotency-Key: 390a99ec-a0ea-4522-b8a9-bca93f0d4745

{
  "sourceWalletId": "SOURCE_WALLET_ID",
  "destinationWalletId": "DESTINATION_WALLET_ID",
  "amount": 30.00
}
```

### Same key + same payload

```text
First request
→ executes transfer
→ persists Transfer X

Retry
→ finds Transfer X
→ does not debit again
→ returns Transfer X
```

### Same key + different payload

```text
Idempotency-Key: X
Amount: 30
→ accepted

Idempotency-Key: X
Amount: 50
→ 409 Conflict
```

### Concurrent duplicate requests

A preliminary `SELECT` improves the normal retry path, but it is not enough to guarantee correctness:

```text
Request A                 Request B
SELECT X                  SELECT X
not found                 not found
```

The final guarantee comes from a unique PostgreSQL constraint on the idempotency key.

If two requests race:

```text
Request A                 Request B
INSERT X                  INSERT X
   |                         |
 success                unique violation
   |                         |
 COMMIT                   ROLLBACK
                             |
                       read winner
```

For the same payload, both callers ultimately receive the same persisted transfer while the financial effect happens only once.

## Optimistic Concurrency

Idempotency protects against the same logical command being executed more than once.

It does not solve two different valid transfers competing for the same wallet balance.

Consider:

```text
Initial balance: 100

Transfer A
Amount: 80
Idempotency-Key: A

Transfer B
Amount: 80
Idempotency-Key: B
```

Both requests may initially read a balance of `100`.

Without concurrency protection, both could independently conclude that enough balance exists.

TransferFlow uses PostgreSQL's `xmin` system column as an optimistic concurrency token through EF Core.

```text
Request A                    Request B
reads wallet version 10      reads wallet version 10
debits 80                    debits 80
       |                            |
UPDATE ... xmin = 10         UPDATE ... xmin = 10
       |                            |
 succeeds                    concurrency conflict
       |
new version
```

When EF Core detects that the expected row version no longer matches, the failed operation is discarded and the complete transfer use case is retried.

The retry reloads current database state and re-evaluates the business rules.

If the winning transfer already consumed the available balance, the retried transfer fails with insufficient funds instead of overspending the wallet.

### Why retry the whole operation?

Retrying only `SaveChangesAsync()` would attempt to persist decisions made from stale state.

The business operation must be executed again:

```text
load current wallets
↓
validate current balance
↓
apply debit and credit
↓
stage Transfer
↓
stage OutboxMessage
↓
commit
```

This keeps concurrency handling aligned with the current state of the database.

## Transactional Outbox

Publishing an integration event directly after committing the database creates a failure window.

A naive implementation could look like:

```text
SaveChangesAsync()
↓
SendMessageAsync()
```

This introduces two inconsistent outcomes.

### Database commits, message publication fails

```text
PostgreSQL
COMMIT ✅

      ↓

SQS publish ❌
```

The transfer exists, but downstream consumers may never learn about it.

### Message publishes, database commit fails

Reversing the order is not safe either:

```text
SQS publish ✅

      ↓

PostgreSQL
COMMIT ❌
```

Consumers could receive an event for a transfer that was never persisted.

PostgreSQL and Amazon SQS do not participate in the same local ACID transaction.

TransferFlow solves this with the Transactional Outbox pattern.

```text
CreateTransferUseCase
        |
        +--> update wallets
        |
        +--> add Transfer
        |
        +--> add OutboxMessage
        |
        v
   SaveChangesAsync()
        |
        v
single PostgreSQL transaction
```

The financial state and the event intent are therefore committed together.

After the transaction succeeds, a background processor reads pending outbox records and publishes them to SQS:

```text
outbox_messages
      |
      v
OutboxProcessor
      |
      v
Amazon SQS
```

When publication succeeds, the outbox record is marked as processed.

If publication fails, the record remains pending and can be attempted again later.

### Delivery semantics

There is still an unavoidable failure window:

```text
publish to SQS succeeds
↓
process crashes before marking outbox row as processed
```

After restart, the same logical event may be published again.

For that reason, TransferFlow does not assume exactly-once delivery.

The messaging design uses:

```text
at-least-once delivery
+
idempotent consumers
```

instead.

## Idempotent Consumer

Amazon SQS provides at-least-once delivery semantics, so the same logical message may be delivered more than once.

Duplicate delivery can happen for several reasons, including:

```text
consumer processes message
↓
database commit succeeds
↓
SQS acknowledgement fails
↓
message becomes visible again
```

As consumers gain side effects, duplicate delivery must not cause those effects to execute more than once.

TransferFlow establishes this idempotency boundary by persisting a stable logical `message-id` for every successfully processed event.

The consumer persists that identifier in PostgreSQL:

```text
processed_messages

message_id
processed_at_utc
```

Before applying the message, the processor checks whether the logical message was already handled.

```text
message received
      |
      v
message-id already processed?
      |
   +--+--+
   |     |
  yes    no
   |     |
 no-op   process
   |     |
   |     v
   |  persist message-id
   |     |
   +-----+
      |
      v
acknowledge SQS message
```

The database primary key on `message_id` is the final uniqueness guarantee.

The preliminary existence check improves the common duplicate path, but correctness does not depend on that check alone.

This makes repeated deliveries safe without requiring exactly-once delivery from the broker.

## Retries and Dead-Letter Queue

A failed message is not immediately discarded.

TransferFlow relies on SQS redelivery for retry behavior:

```text
message received
      |
      v
processing fails
      |
      v
message is not deleted
      |
      v
visibility timeout expires
      |
      v
message becomes visible again
```

This is useful for transient failures such as a temporarily unavailable database.

After repeated failed deliveries, SQS moves the message to a dead-letter queue.

The local environment configures a redrive policy with:

```text
maxReceiveCount = 3
```

Conceptually:

```text
delivery 1 → fail
delivery 2 → fail
delivery 3 → fail
...
receive threshold exceeded
        |
        v
       DLQ
```

The DLQ prevents permanently failing messages from being retried forever and gives operators a place to inspect poison messages.

TransferFlow intentionally does not add an in-process retry library around the consumer yet.

SQS redelivery is enough for the current failure model and avoids introducing another retry layer before it is justified.

## Observability and Correlation

Distributed flows become difficult to investigate if each component logs independently without a shared identifier.

TransferFlow propagates a correlation ID across the request and messaging flow.

```text
HTTP Request
X-Correlation-ID
      |
      v
ASP.NET Core
      |
      v
Application
      |
      v
OutboxMessage
      |
      v
SQS message attribute
      |
      v
Consumer
```

If the client provides a valid `X-Correlation-ID`, TransferFlow reuses it.

Otherwise, the API generates a new identifier.

The correlation ID is used for observability only. It is not a business identifier and does not provide idempotency.

### Identifier responsibilities

```text
CorrelationId
→ which distributed flow does this operation belong to?

Idempotency-Key
→ has this logical HTTP command already been executed?

TransferId
→ which persisted transfer is this?

message-id
→ which logical integration event is this?

SQS MessageId
→ which physical SQS message delivery is this?
```

This distinction is important because these identifiers solve different problems.

A retried HTTP request may therefore have:

```text
CorrelationId: C2
Idempotency-Key: K1
```

even if the original request used:

```text
CorrelationId: C1
Idempotency-Key: K1
```

The idempotency key still identifies the same logical command, while correlation IDs describe separate execution flows.

## Structured Logging

Logging is kept close to the layer that owns the relevant information.

```text
Api / Middleware
→ HTTP context and request correlation

Application
→ meaningful use-case events

Infrastructure
→ persistence, Outbox and messaging boundaries

Domain
→ no logging dependency
```

TransferFlow uses structured log properties such as:

```text
CorrelationId
TransferId
OutboxMessageId
EventType
MessageId
SqsMessageId
```

rather than embedding all context only inside free-form log messages.

The project also avoids logging message payloads, credentials, connection strings and other sensitive infrastructure details.

## API

### Wallets

```http
POST /wallets
```

Creates a wallet.

```http
GET /wallets/{id}
```

Returns a wallet by ID.

### Transfers

```http
POST /transfers
Idempotency-Key: <key>
```

Creates a wallet-to-wallet transfer.

```http
GET /transfers/{id}
```

Returns a transfer by ID.

### Health

```http
GET /health/live
```

Liveness verifies that the application process is running.

It intentionally does not check external dependencies.

```http
GET /health/ready
```

Readiness verifies whether the API is ready to serve requests.

PostgreSQL is part of readiness because the main API operations depend on it.

Amazon SQS is intentionally not part of API readiness.

The Transactional Outbox allows transfers to be committed while SQS is temporarily unavailable, so a broker outage does not automatically mean the HTTP API must stop accepting traffic.

```http
GET /health
```

Currently exposes the same readiness-oriented health information for convenience.

## Manual API Example

Create two wallets:

```http
POST /wallets
```

Example response:

```json
{
  "id": "f2ee139a-737b-4726-98d0-3eaebce92a73",
  "balance": 0
}
```

```http
POST /wallets
```

Example response:

```json
{
  "id": "e787d809-dd8f-4915-b97c-e7d4e94aa700",
  "balance": 0
}
```

Wallets currently start with zero balance.

TransferFlow intentionally does not implement a deposit or top-up endpoint because funding workflows are outside the current project scope.

For local manual testing, a source wallet can be funded directly in the development database:

```sql
UPDATE wallets
SET balance = 100.00
WHERE id = 'f2ee139a-737b-4726-98d0-3eaebce92a73';
```

Then create a transfer:

```http
POST /transfers
Content-Type: application/json
Idempotency-Key: 7b2b4e5a-1234-4bc3-a81d-8888cf34aa01
X-Correlation-ID: 11111111-1111-1111-1111-111111111111

{
  "sourceWalletId": "f2ee139a-737b-4726-98d0-3eaebce92a73",
  "destinationWalletId": "e787d809-dd8f-4915-b97c-e7d4e94aa700",
  "amount": 30.00
}
```

Example response:

```json
{
  "id": "51fe5e2f-58c7-4e0d-942b-87b335218876",
  "sourceWalletId": "f2ee139a-737b-4726-98d0-3eaebce92a73",
  "destinationWalletId": "e787d809-dd8f-4915-b97c-e7d4e94aa700",
  "amount": 30.00,
  "createdAtUtc": "2026-09-21T20:00:00Z"
}
```

Repeating the same request with the same `Idempotency-Key` returns the already persisted logical transfer instead of debiting the wallet again.

Reusing the same key with a different payload is rejected.

The repository also contains `TransferFlow.Api.http` with ready-to-edit HTTP requests for local development.

## Interview Talking Points

### What was the most important architectural decision?

The Transactional Outbox is one of the most important decisions in TransferFlow.

Persisting the transfer and publishing directly to SQS would create a consistency gap between PostgreSQL and the broker.

By storing the event intent in PostgreSQL within the same transaction as the financial changes, TransferFlow guarantees that a committed transfer also has a durable event waiting for publication.

The trade-off is eventual delivery and the possibility of duplicates, which are handled through idempotent consumers.

### How is concurrency different from idempotency?

They protect against different problems.

```text
Idempotency
→ same logical command repeated

Concurrency control
→ different operations modifying stale shared state
```

An `Idempotency-Key` prevents the same transfer command from taking effect twice.

PostgreSQL `xmin` protects wallet balances when different transfers concurrently operate on the same wallet.

### Why retry the entire use case after a concurrency conflict?

A concurrency conflict means the operation made decisions using stale state.

Retrying only the database commit would preserve those stale decisions.

TransferFlow reloads the wallets and executes the complete business operation again so validation and balance checks use current state.

### Why not use exactly-once messaging?

PostgreSQL and SQS do not share a single ACID transaction, and SQS uses at-least-once delivery semantics.

TransferFlow therefore assumes duplicates are possible and designs for:

```text
durable Outbox
+
at-least-once delivery
+
idempotent consumer
```

rather than depending on an exactly-once guarantee.

### What would change at production scale?

Important additions would include:

* authentication, authorization and wallet ownership
* explicit currency modeling
* metrics and distributed tracing
* Outbox backlog and DLQ monitoring
* stronger SQS resilience policies
* safe parallel Outbox processing
* secret management
* deployment automation
* operational tooling for replaying failed messages
* independent worker deployment if scaling requirements justify it

### Where did the project deliberately avoid overengineering?

TransferFlow intentionally avoids several abstractions and infrastructure choices that are not currently justified:

* no generic repository abstraction
* no CQRS framework
* no microservice split
* no exactly-once messaging claim
* no additional in-process retry library around SQS consumption
* no LocalStack dependency in CI when the current integration tests do not require a real broker
* no distributed transaction between PostgreSQL and SQS

The project introduces complexity only when a concrete failure mode or requirement justifies it.

## Local Development

### Requirements

* .NET 10 SDK
* Docker with Docker Compose
* EF Core CLI (`dotnet-ef`) for database migrations
* Visual Studio with .NET 10 support, or another compatible IDE

### 1. Clone the repository

```bash
git clone https://github.com/renangiusti02/transferflow.git
cd transferflow
```

### 2. Start local infrastructure

Copy:

```text
.env.example
```

to:

```text
.env
```

Configure the PostgreSQL credentials and LocalStack authentication token for your environment.

Then start the infrastructure:

```bash
docker compose up -d
```

Docker Compose starts:

```text
PostgreSQL
→ localhost:5432

LocalStack
→ localhost:4566
```

When LocalStack becomes ready, `docker/localstack/init-sqs.sh` automatically creates:

```text
transfer-completed
        |
        | maxReceiveCount = 3
        v
transfer-completed-dlq
```

The main queue is configured with a redrive policy so messages that repeatedly fail processing are eventually moved to the dead-letter queue.

### 3. Configure the API connection string

TransferFlow keeps the application connection string outside source control using .NET User Secrets.

Example:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=transferflow;Username=transferflow;Password=YOUR_PASSWORD" --project src/TransferFlow.Api
```

Use credentials matching your local PostgreSQL environment.

### 4. Apply migrations

Using EF Core CLI:

```bash
dotnet ef database update \
  --project src/TransferFlow.Infrastructure \
  --startup-project src/TransferFlow.Api \
  --context TransferFlowDbContext
```

Migrations can also be managed through Visual Studio's Package Manager Console.

### 5. Run the API

The Development environment already points the SQS integration to LocalStack:

```text
Queue:      transfer-completed
Region:     us-east-1
ServiceUrl: http://localhost:4566
```

Run:

```bash
dotnet run --project src/TransferFlow.Api
```

The API, Outbox publisher and SQS consumer run in the same application process for the current project scope.

## Tests

Run the complete test suite with:

```bash
dotnet test TransferFlow.slnx
```

The solution contains three testing levels.

### Domain Tests

Validate business invariants without infrastructure.

```text
TransferFlow.Domain.Tests
```

Examples include wallet balance rules and transfer invariants.

### Application Tests

Validate use-case orchestration using lightweight fakes.

```text
TransferFlow.Application.Tests
```

These tests focus on behavior such as validation, idempotency and orchestration without requiring PostgreSQL.

### Integration Tests

Validate the real EF Core + PostgreSQL behavior.

```text
TransferFlow.Integration.Tests
```

These tests cover scenarios including:

* wallet persistence
* transactional transfer persistence
* rollback behavior
* HTTP idempotency constraints
* optimistic concurrency
* concurrent transfers competing for the same balance
* Transactional Outbox persistence
* Outbox processing
* idempotent consumer persistence and redelivery behavior

A shared `PostgreSqlIntegrationTestFixture` creates `DbContext` instances and applies EF Core migrations before the integration test suite executes.

The tests deliberately use PostgreSQL instead of SQLite or an EF in-memory provider because important behavior is provider-specific, including:

```text
PostgreSQL xmin concurrency
unique constraints
transaction behavior
relational persistence
```

### Local test database

Locally, configure the integration test connection string using User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=transferflow;Username=transferflow;Password=YOUR_PASSWORD" --project tests/TransferFlow.Integration.Tests
```

The test infrastructure can also read:

```text
ConnectionStrings__Database
```

from an environment variable.

This is the mechanism used by CI.

## Continuous Integration

GitHub Actions validates every pull request targeting `main` and every push to `main`.

The CI job runs on a clean Ubuntu runner and starts a disposable PostgreSQL 18 service.

```text
checkout
↓
setup .NET 10
↓
start PostgreSQL 18
↓
restore
↓
build Release
↓
run complete test suite
```

The CI database starts empty.

The integration test fixture applies the real EF Core migrations before executing the PostgreSQL integration tests.

This verifies that the project does not depend on developer-machine state such as an already prepared database or local User Secrets.

LocalStack is intentionally not part of the current CI pipeline.

The existing SQS processor integration tests validate message-processing behavior against real PostgreSQL while constructing the SQS message contract in-process. Full broker-level integration can be introduced later if the additional confidence justifies the extra test infrastructure.

## Current Engineering Decisions

### Specific repositories instead of a generic repository

Persistence abstractions are introduced around concrete needs rather than hiding EF Core behind a generic CRUD abstraction.

### Unit of Work

`IUnitOfWork` defines the commit boundary used by Application.

The EF implementation also translates infrastructure-specific persistence failures without exposing EF Core or Npgsql exceptions to the Application layer.

### PostgreSQL constraints complement Domain rules

Important invariants are protected both in application code and, where appropriate, at database level.

### Idempotency is currently part of Transfer

Because idempotency currently belongs only to transfer creation and has a natural one-to-one relationship with a transfer, the key is stored directly with the transfer.

If idempotency becomes a cross-cutting concern across multiple commands, it can later be extracted into a dedicated idempotency/inbox model.

### PostgreSQL `xmin` for optimistic concurrency

Wallet balance updates use PostgreSQL's `xmin` system column as the EF Core concurrency token.

This avoids introducing an application-managed version column while still allowing EF Core to detect stale updates.

The trade-off is that this implementation is intentionally PostgreSQL-specific.

If database portability became a requirement, an explicit application-managed version column would be a more portable alternative.

### Whole-use-case retry instead of persistence-only retry

When a concurrency conflict occurs, TransferFlow retries the complete transfer operation rather than calling `SaveChangesAsync()` again with stale entities.

This ensures that wallet balances and business rules are re-evaluated against current database state.

The retry count is deliberately bounded to avoid infinite contention loops.

### Transactional Outbox instead of direct broker publication

TransferFlow does not publish to SQS directly from the transfer use case after committing PostgreSQL.

Instead, the integration event is persisted as an Outbox record in the same transaction as the transfer.

This favors consistency between business state and event intent at the cost of eventual rather than immediate message publication.

### At-least-once delivery instead of exactly-once assumptions

The system accepts that Outbox publication and SQS delivery may produce duplicates.

Rather than attempting to provide exactly-once delivery across PostgreSQL and SQS, consumers are designed to be idempotent.

This keeps the consistency model explicit and realistic.

### PostgreSQL as the real integration-test database

Integration tests intentionally run against PostgreSQL rather than SQLite or EF Core's in-memory provider.

This increases test infrastructure cost slightly, but provides coverage for behavior that matters to TransferFlow:

* `xmin` concurrency
* relational constraints
* PostgreSQL transaction behavior
* real EF Core/Npgsql mappings

### SQS is not part of API readiness

A temporary SQS outage does not prevent a transfer from being safely committed because the Outbox retains the event for later publication.

PostgreSQL is therefore a readiness dependency for the HTTP API, while SQS is treated as an asynchronous dependency that should be monitored separately.

### One deployable application instead of microservices

The API, Outbox background processor and SQS consumer currently run in the same application process.

This is intentional.

The project demonstrates asynchronous messaging and reliable delivery without introducing distributed deployment complexity that the current scope does not require.

The components could be separated later if independent scaling, deployment or fault isolation became necessary.

## Trade-offs and Production Considerations

TransferFlow intentionally focuses on backend consistency and messaging patterns rather than implementing every concern required by a real financial platform.

For a production system, areas that would require additional work include:

### Authentication and authorization

The current API does not implement user authentication, wallet ownership or authorization policies.

A real system would need to ensure callers can only operate on resources they are allowed to access.

### Monetary representation and currencies

The project currently models balances using `decimal` and assumes a single implicit currency.

A multi-currency system would need explicit currency semantics and stronger modeling around monetary values.

### Outbox processing at scale

The current Outbox processor is suitable for the scope of this project.

At higher throughput, considerations would include:

* safely claiming batches across multiple workers
* database locking strategy
* backpressure
* partitioning
* monitoring publication lag

### Consumer recovery and DLQ operations

Messages can be moved to a DLQ, but production operations would also need tooling and procedures for:

* investigating poison messages
* replaying messages safely
* alerting on DLQ growth
* tracking repeated failures

### Resilience around external infrastructure

SQS receive and publication operations would need a more complete resilience policy for prolonged outages, throttling and network failures.

Retries should be designed carefully to avoid creating retry storms.

### Observability

The current implementation establishes structured logging and correlation.

A production deployment would typically extend this with:

* metrics
* distributed tracing
* dashboards
* alerts
* Outbox backlog monitoring
* DLQ monitoring

### Deployment and secrets

Local configuration uses Docker Compose and .NET User Secrets.

A production deployment would require proper secret management, environment-specific configuration, infrastructure provisioning and deployment automation.

## Roadmap

### Transfer Core

* [x] Wallet domain model
* [x] PostgreSQL persistence
* [x] Atomic wallet transfers
* [x] HTTP idempotency
* [x] Optimistic wallet concurrency
* [x] Whole-operation retry after concurrency conflicts

### Reliable Messaging

* [x] Transactional Outbox
* [x] Publish transfer events to Amazon SQS
* [x] Local SQS development with LocalStack
* [x] Idempotent SQS message processing
* [x] SQS redelivery and dead-letter queue
* [x] Correlation propagation across asynchronous messaging

### Quality

* [x] Structured logging
* [x] Correlation IDs
* [x] Liveness and readiness health checks
* [x] PostgreSQL integration test environment
* [x] GitHub Actions CI

### Portfolio

* [x] Architecture documentation
* [x] System design and trade-off documentation
* [ ] DynamoDB activity projection as an optional extension

## Learning Goals

TransferFlow is intentionally built incrementally to explore:

* ASP.NET Core backend development
* dependency injection and object lifetimes
* async/await and cancellation
* Entity Framework Core
* PostgreSQL transactions
* ACID properties
* optimistic concurrency
* race conditions
* HTTP idempotency
* Transactional Outbox
* asynchronous messaging
* idempotent consumers
* observability
* unit and integration testing
* backend system design
