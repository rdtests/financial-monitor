# Real-Time Financial Monitor

[![CI](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml)

MVP of a real-time financial monitor: an HTTP API ingests transactions, stores the latest ones in memory,
and pushes every change to a live dashboard used by support agents.

## Repository layout

```
backend/    .NET 8 solution (src/ + tests/)
frontend/   React + TypeScript (Vite)            – coming soon
docs/       Assignment and implementation plan
```

## Prerequisites

- .NET SDK 8.0 or newer (the projects target `net8.0`)

## Running the backend

```bash
cd backend
dotnet run --project src/FinancialMonitor.Api
```

The API listens on `http://localhost:5080`.

## Tests

```bash
cd backend
dotnet test
```

Developed test-first: each step's tests were written and seen failing before the implementation.

Tests also run automatically on every push via GitHub Actions (`.github/workflows/ci.yml`).

## Domain model

A transaction follows the assignment's JSON structure:

```json
{ "transactionId": "guid", "amount": 1500.50, "currency": "USD", "status": "Pending | Completed | Failed", "timestamp": "2024-01-15T10:00:00Z" }
```

- `transactionId` is the key: a second transaction with the same id is an **update** of the first
  (e.g. its status moves from `Pending` to `Completed`), not a new record.
- `amount` is `decimal`, never `double`, so money values are not affected by binary floating-point rounding.
- `Transaction` is an immutable `record`; an update replaces the stored instance.
- Storage is in memory (`InMemoryTransactionRepository`, behind `ITransactionRepository`).

## Backend architecture

```
TransactionService ── ITransactionRepository  (store)
                   └─ ITransactionNotifier    (broadcast via SignalR hub "TransactionUpdated")
```

The service stores a transaction first and only then broadcasts it, so clients never see data that was not saved.
Each dependency sits behind an interface and is injected, so it can be replaced (e.g. a shared repository) or
substituted in tests.

## Key Decisions

- **Walking skeleton first.** A thin end-to-end slice (form → API → storage → SignalR → dashboard) is built
  before any layer is hardened, so integration risks (proxy, SignalR, JSON field names) surface early and
  there is always a demoable system.
- **SignalR, not raw WebSockets.** SignalR gives automatic reconnect, transport fallback and a thread-safe
  broadcast API (`IHubContext`) out of the box, plus a built-in Redis backplane for running several pods.
