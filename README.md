# Real-Time Financial Monitor

[![CI](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml)

MVP of a real-time financial monitor: an HTTP API ingests transactions, stores the latest ones in memory,
and pushes every change to a live dashboard used by support agents.

## Repository layout

```
backend/    .NET 8 solution (src/ + tests/)
frontend/   React + TypeScript (Vite), organized by feature (src/features/simulator, src/features/monitor)
docs/       Assignment and implementation plan
```

## Prerequisites

- .NET SDK 8.0 or newer (the projects target `net8.0`)
- Node.js 22 or newer

## Running the backend

```bash
cd backend
dotnet run --project src/FinancialMonitor.Api
```

The API listens on `http://localhost:5080`:

| Endpoint | Purpose |
|----------|---------|
| `POST /api/transactions` | Ingest a transaction. `201` with `{ transactionId, outcome: "Created" }` for a new id, `200` with `outcome: "Updated"` for an existing one, `400` for invalid input (see [Validation](#validation)) |
| `GET /api/transactions` | Snapshot of the latest transactions (used by the dashboard on load) |
| `/hubs/transactions` | SignalR hub; pushes a `TransactionUpdated` event with every change |
| `GET /healthz` | Health check for Kubernetes probes |
| `/swagger` | Swagger UI for trying the API from the browser (Development only) |

Quick try from a terminal:

```bash
curl -X POST http://localhost:5080/api/transactions -H "Content-Type: application/json" \
  -d '{"transactionId":"3f2b8c1e-4d5a-4b6c-8e9f-0a1b2c3d4e5f","amount":1500.50,"currency":"USD","status":"Pending","timestamp":"2024-01-15T10:00:00Z"}'
```

## Running the frontend

With the backend running, in a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Open two browser tabs:

- `http://localhost:5173/monitor` – Live Dashboard
- `http://localhost:5173/add` – Transaction Simulator

A transaction sent from the simulator appears on the dashboard immediately. The Vite dev server proxies
`/api` and `/hubs` (including the WebSocket upgrade) to the backend on port 5080, so the browser talks to a
single origin.

**How the dashboard loads:** it connects to the SignalR hub first and only then fetches the
`GET /api/transactions` snapshot, so no event can be missed between the two. Rows are kept in a map keyed by
`transactionId`, so an update replaces its row instead of adding a new one.

## Tests

```bash
cd backend
dotnet test

cd frontend
npm test
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
TransactionsController  (HTTP only: TransactionRequest → Transaction, outcome → status code)
  → TransactionService ── ITransactionRepository  (store)
                       └─ ITransactionNotifier    (broadcast via SignalR hub "TransactionUpdated")
```

The service stores a transaction first and only then broadcasts it, so clients never see data that was not saved.
Each dependency sits behind an interface and is injected, so it can be replaced (e.g. a shared repository) or
substituted in tests.

The domain `Transaction` never crosses the API boundary: requests arrive as `TransactionRequest` and leave as
`TransactionResponse` (the same shape for the GET snapshot and the SignalR event). `status` is serialized as its
name (`"Completed"`), read case-insensitively; numeric values are rejected.

### Validation

Every POST is validated before it reaches the controller. An invalid request gets `400 Bad Request` with a
`ValidationProblemDetails` body listing an error per field:

| Rule | Error key |
|------|-----------|
| All five fields are required | the field, e.g. `amount` |
| `transactionId` is not the empty GUID | `transactionId` |
| `amount` is greater than 0 | `amount` |
| `currency` is 3 uppercase letters (`USD`) | `currency` |
| `status` is `Pending`, `Completed` or `Failed` (any case) | `$.status` |
| Malformed JSON or a wrong type (`"amount": "abc"`) | the JSON path, e.g. `$.amount` |

```json
{ "status": 400, "title": "One or more validation errors occurred.",
  "errors": { "amount": ["The Amount field is required."], "currency": ["Currency must be a 3-letter uppercase ISO code, e.g. USD."] } }
```

Field rules run first; the cross-field checks (`transactionId` not empty, `amount` > 0) run once those pass.

## Security

- No authentication in this MVP.
- The API accepts transaction data via POST.
- In production, authentication and authorization are required.
- Access to data should be restricted by user role.

## Key Decisions

- **Walking skeleton first.** A thin end-to-end slice (form → API → storage → SignalR → dashboard) is built
  before any layer is hardened, so integration risks (proxy, SignalR, JSON field names) surface early and
  there is always a demoable system.
- **SignalR, not raw WebSockets.** SignalR gives automatic reconnect, transport fallback and a thread-safe
  broadcast API (`IHubContext`) out of the box, plus a built-in Redis backplane for running several pods.
- **API contract (DTO) separate from the domain model.** Request fields are nullable, so a missing `amount` is
  not silently read as `0`; the domain `Transaction` stays an immutable, always-valid record; and the external
  contract can change without touching business logic.
- **No CORS.** The frontend and the API are always served from the same origin (the Vite proxy in development,
  the same container in production), so a CORS policy would be code that does nothing.
- **Controller and notifier are covered by integration tests, not mocked unit tests.** Both are thin layers;
  the value is in testing them against the real pipeline (routing, model binding, JSON, SignalR) with
  `WebApplicationFactory`.
- **Validation with DataAnnotations and an enum in the DTO, not a hand-written validator.** Less code, and every
  kind of bad input (missing field, invalid value, broken JSON) produces the same `ValidationProblemDetails`
  format, so the frontend parses a single shape. Validation is tested through HTTP, where it actually runs.
- **Frontend organized by feature.** Each route is a self-contained slice (`features/simulator` for `/add`,
  `features/monitor` for `/monitor`) with its own components, hooks and state; only code both need lives in
  `shared/`.
- **API types written by hand** in `frontend/src/shared/api/types.ts`. With two endpoints and five fields this is
  simpler than code generation and adds no build step. In a larger system the types would be generated from the
  OpenAPI document, to prevent drift between client and server.
