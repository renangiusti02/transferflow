# TransferFlow

TransferFlow is a backend-focused wallet transfer API built with **.NET 10, ASP.NET Core, Entity Framework Core, and PostgreSQL**.

The project is designed as a compact engineering playground for building and demonstrating backend concepts that matter in financial and distributed systems: **atomic transactions, idempotency, concurrency control, reliable messaging, observability, and automated testing**.

> TransferFlow is a portfolio and learning project. It is not intended to be a production banking or payment platform.

## Current Features

* Create and retrieve wallets
* Transfer balance between wallets
* Persist wallet and transfer changes atomically
* Reject invalid transfers and insufficient balances
* Persist transfer history in PostgreSQL
* Idempotent `POST /transfers` using `Idempotency-Key`
* Return the original transfer when the same request is retried
* Reject reuse of an idempotency key with a different payload
* Protect concurrent duplicate requests with a PostgreSQL unique constraint
* Unit tests for Domain and Application layers
* Integration tests against real PostgreSQL
* Deterministic concurrency tests using independent `DbContext` instances
* Health check endpoint
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

```text
                    HTTP
                     |
                     v
             TransferFlow.Api
                     |
                     v
          TransferFlow.Application
              /             \
             v               v
   TransferFlow.Domain    Interfaces
                             ^
                             |
               TransferFlow.Infrastructure
                             |
                    EF Core / Npgsql
                             |
                         PostgreSQL
```

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

The API acts as the composition root and configures dependency injection.

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
* **Docker Compose**
* **xUnit**

Planned stages introduce AWS-oriented messaging and additional production engineering patterns.

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
GET /health
```

Returns the application health status.

## Local Development

### Requirements

* .NET 10 SDK
* PostgreSQL or Docker
* Docker Compose
* Visual Studio with .NET 10 support, or another compatible IDE

### 1. Clone the repository

```bash
git clone https://github.com/renangiusti02/transferflow.git
cd transferflow
```

### 2. Configure PostgreSQL

Copy:

```text
.env.example
```

to:

```text
.env
```

Then start PostgreSQL:

```bash
docker compose up -d
```

The current Compose environment exposes PostgreSQL on port `5432`.

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

```bash
dotnet run --project src/TransferFlow.Api
```

## Tests

Run the complete test suite with:

```bash
dotnet test TransferFlow.slnx
```

The solution contains three testing levels:

### Domain Tests

Validate business invariants without infrastructure.

```text
TransferFlow.Domain.Tests
```

### Application Tests

Validate use-case orchestration using lightweight fakes.

```text
TransferFlow.Application.Tests
```

### Integration Tests

Validate EF Core and PostgreSQL behavior using a real database.

```text
TransferFlow.Integration.Tests
```

Integration tests include concurrent transfer scenarios where separate `DbContext` instances simulate independent requests competing against the same PostgreSQL database.

Configure their connection string using User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=transferflow;Username=transferflow;Password=YOUR_PASSWORD" --project tests/TransferFlow.Integration.Tests
```

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

## Roadmap

### Transfer Core

* [x] Wallet domain model
* [x] PostgreSQL persistence
* [x] Atomic wallet transfers
* [x] API idempotency
* [ ] Protect wallet balances against different concurrent transfers

### Reliable Messaging

* [ ] Transactional Outbox
* [ ] Publish transfer events to Amazon SQS
* [ ] Local SQS development with LocalStack
* [ ] Idempotent message consumer
* [ ] Retry and dead-letter queue handling

### Quality

* [ ] Structured logging
* [ ] Correlation IDs
* [ ] Extended health checks
* [ ] Automated integration test environment
* [ ] GitHub Actions CI

### Portfolio

* [ ] Architecture documentation
* [ ] System design notes
* [ ] DynamoDB activity projection as an optional extension

## Known Next Problem

Idempotency prevents the **same logical request** from executing more than once.

It does not yet protect this scenario:

```text
Initial balance: 100

Transfer A
Idempotency-Key: A
Amount: 80

Transfer B
Idempotency-Key: B
Amount: 80
```

These are two different legitimate requests competing for the same wallet balance.

Handling that race safely is the next concurrency problem in the project.

## Learning Goals

TransferFlow is intentionally built incrementally to explore:

* ASP.NET Core backend development
* dependency injection and object lifetimes
* async/await and cancellation
* Entity Framework Core
* PostgreSQL transactions
* ACID properties
* optimistic and pessimistic concurrency
* HTTP idempotency
* race conditions
* Transactional Outbox
* asynchronous messaging
* idempotent consumers
* observability
* unit and integration testing
* backend system design
