# Real-Time Financial Monitor

[![CI](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/rdtests/financial-monitor/actions/workflows/ci.yml)

MVP of a real-time financial monitor: an HTTP API ingests transactions, stores the latest ones in memory,
and pushes every change to a live dashboard used by support agents.

- **Backend:** .NET 8, ASP.NET Core, SignalR, in-memory storage
- **Frontend:** React + TypeScript (Vite), React Router
- **Tests:** xUnit, Shouldly, NSubstitute; Vitest

## Quick start

**Prerequisites:** .NET SDK 8.0 or newer, Node.js 22.12 or newer.

1. Start the backend:

   ```bash
   cd backend
   dotnet run --project src/FinancialMonitor.Api
   ```

   The API listens on `http://localhost:5080` (Swagger UI at `http://localhost:5080/swagger`).

2. In a second terminal, start the frontend:

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

3. Open two browser tabs:
   - `http://localhost:5173/monitor` – Live Dashboard
   - `http://localhost:5173/add` – Transaction Simulator

   A transaction sent from the simulator appears on the dashboard immediately.

The Vite dev server proxies `/api` and `/hubs` (including the WebSocket upgrade) to the backend, so the browser
talks to a single origin.

### Running with Docker

```bash
docker build -t financial-monitor .
docker run -p 8080:8080 financial-monitor
```

Open `http://localhost:8080/monitor` and `http://localhost:8080/add`. In the container the API also serves the
built frontend, so UI, API and SignalR share one origin.

The [`Dockerfile`](Dockerfile) is multi-stage: the frontend (`node:24-alpine`) and the backend (.NET SDK) are
built in separate stages, and only the published app and the static files are copied into
`aspnet:8.0-alpine` (about 160 MB, no SDK or Node). It runs as the image's non-root `app` user on port 8080,
in the `Production` environment (no Swagger, no stack traces).

### Running the tests

Each from the repository root:

```bash
cd backend
dotnet test
```

```bash
cd frontend
npm test
```

They also run on every push in GitHub Actions ([`ci.yml`](.github/workflows/ci.yml)).

- **Backend:** unit tests for the repository, service and mappings; integration tests through the
  real pipeline with `WebApplicationFactory` (HTTP, validation, SignalR clients, error handling); concurrency
  tests for the repository and for parallel HTTP requests.
- **Frontend:** Vitest for pure logic only (reducer, API client, filter, batcher), where bugs hide,
  without the cost of component tests.

Developed test-first: new behavior was driven by tests that were written and seen failing before the
implementation. A few tests that guard behavior already in place (e.g. a status transition, or a framework
feature such as rejecting malformed JSON) passed from the start and are kept as regression tests.

## Features

### Transaction Simulator (`/add`)

Plays the external system that feeds data into the engine:

- **Send** – a new transaction from the form. The form does no client-side validation on purpose (`noValidate`):
  the server is the source of truth, so its `400` errors appear next to each field (try an empty amount or `0`).
- **Send random** – a random transaction (mostly `Pending`).
- **Send burst of 100** – 100 random transactions in parallel, for checking that the dashboard stays responsive.
  The result reports how many succeeded and failed (`Promise.allSettled`).
- **Last sent** – follow-up updates for the last accepted transaction (same `transactionId`): **Complete** /
  **Fail** walk the lifecycle (`Pending → Completed / Failed`), and **Stale update** sends an older timestamp,
  which the server answers with `IgnoredStale` (shown as information, not an error).

### Live Dashboard (`/monitor`)

- **Real-time list**, newest first; an update replaces its row instead of adding one.
- **Status badges** – Completed green, Failed red, Pending orange, each with an icon and text so the status
  never relies on color alone.
- **Filter** – All / Pending / Completed / **Errors only** (Failed), purely client-side.
- **Connection status** – `Live`, or `Reconnecting…` while the connection is down. Reconnect retries forever
  (1s, 2s, 4s, 8s, then every 10s); SignalR's default gives up after 4 attempts, which would leave a dashboard
  that stays open all day silently disconnected. There is therefore no Reconnect button.
- **Snapshot load failure** – a banner with a Retry button; live events keep arriving meanwhile.

## API

| Endpoint | Purpose |
|----------|---------|
| `POST /api/transactions` | Ingest a transaction (see responses below) |
| `GET /api/transactions` | Snapshot of the latest transactions (used by the dashboard on load) |
| `/hubs/transactions` | SignalR hub; pushes a `TransactionUpdated` event with every change |
| `GET /healthz` | Health check for Kubernetes probes |
| `/swagger` | Swagger UI for trying the API from the browser (Development only) |

A transaction follows the assignment's JSON structure, in requests, responses and SignalR events alike:

```json
{ "transactionId": "guid", "amount": 1500.50, "currency": "USD", "status": "Pending | Completed | Failed", "timestamp": "2024-01-15T10:00:00Z" }
```

`POST` responses:

| Code | When | Body |
|------|------|------|
| `201` | New `transactionId` | `{ "transactionId": "…", "outcome": "Created" }` |
| `200` | Update of an existing id | `outcome: "Updated"` |
| `200` | Update older than the stored one (out-of-order delivery is expected, not an error) | `outcome: "IgnoredStale"` |
| `400` | Invalid input (see [Validation](#validation)) | `ValidationProblemDetails` |
| `500` | Unexpected exception (see [Error handling](#error-handling)) | `ProblemDetails` |

Quick try (bash):

```bash
curl -X POST http://localhost:5080/api/transactions -H "Content-Type: application/json" \
  -d '{"transactionId":"3f2b8c1e-4d5a-4b6c-8e9f-0a1b2c3d4e5f","amount":1500.50,"currency":"USD","status":"Pending","timestamp":"2024-01-15T10:00:00Z"}'
```

## Domain rules

- **`transactionId` is the key.** A second transaction with the same id is an update of the first
  (e.g. `Pending → Completed`), not a new record.
- **`amount` is `decimal`, never `double`,** so money values are not affected by binary floating-point rounding.
- **An older timestamp is ignored.** The external system may deliver updates out of order. An update older than
  the stored one is not saved and not broadcast.
- **An equal timestamp is accepted** as an update, so a `Pending → Completed` change within the same second is
  not lost and a retried request does not fail. Different transactions may share a timestamp.
- **No status lifecycle is enforced.** The timestamp is the only rule, so `Failed → Completed` (e.g. a successful
  retry) is accepted if it is newer. The external system is the source of truth; this is a monitor.
- **Capacity.** The server keeps the latest 1000 transactions (`Transactions:Capacity` in `appsettings.json`).
  When a new transaction arrives at capacity, the one with the oldest timestamp is removed. Without a bound,
  memory would grow forever.

## Backend architecture

```
backend/src/FinancialMonitor.Api
  Controllers/   TransactionsController  HTTP only: TransactionRequest → Transaction, outcome → status code
  Services/      TransactionService      store, then broadcast
  Repositories/  InMemoryTransactionRepository
  Notifiers/     SignalRTransactionNotifier
  Hubs/          TransactionsHub
  Contracts/     DTOs + mappings (the API contract)
  Models/        Transaction, TransactionStatus, UpsertResult (the domain)
```

```
TransactionsController
  → TransactionService ── ITransactionRepository  (store)
                       └─ ITransactionNotifier    (broadcast "TransactionUpdated" via SignalR)
```

- **Store first, then broadcast,** so clients never see data that was not saved.
- **Every dependency sits behind an interface** and is injected, so it can be replaced (e.g. by a shared
  repository) or substituted in tests.
- **The domain `Transaction` never crosses the API boundary:** requests arrive as `TransactionRequest` and leave
  as `TransactionResponse`, the same shape for the GET snapshot and the SignalR event. `status` is serialized as
  its name (`"Completed"`), read case-insensitively; numeric values are rejected.

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

### Error handling

- **Every error response is `ProblemDetails`** (RFC 7807), so clients parse one format.
- **A single global handler** (`AddProblemDetails` + `UseExceptionHandler`) instead of try/catch in controllers.
  An unexpected exception returns a generic `500` with a `traceId` for correlating with the server log, and no
  exception details. In Development, .NET 8 shows the Developer Exception Page with the stack trace instead;
  that is intended for local debugging only. The container runs as `Production`.
- **A failed SignalR broadcast does not fail the request.** The transaction is already stored, so the service
  logs a warning and returns success; dashboards catch up when they reload the snapshot.
- **A client that disconnects** does not affect the request or the other connected clients.
- **Deliberately not used:** `401`/`403` (no authentication in the MVP), `404` (no get-by-id endpoint),
  `409` (a stale update is expected, not a conflict).

### Concurrency

- **Repository:** a single `lock` guards the dictionary. `Upsert` is check-then-act (read the stored timestamp,
  compare, write, maybe evict), so the whole sequence must be atomic. `ConcurrentDictionary.AddOrUpdate` was not
  used: its delegate may run more than once and it cannot cleanly report Created / Updated / IgnoredStale.
  `GetLatest` copies inside the lock and returns a snapshot, so callers never see the internal collection.
- **No I/O under the lock.** The service broadcasts only after `Upsert` returns, so a slow client never blocks
  writers.
- **Broadcasting:** SignalR's `IHubContext` is thread-safe and manages connections itself; there is no shared
  state of our own on the broadcast side.
- **Out-of-order broadcasts:** two concurrent updates of the same id may be broadcast in reverse order. The
  dashboard therefore applies the same timestamp rule (see [Frontend architecture](#frontend-architecture)).

**How it is tested:** the concurrency tests start all threads together on a `Barrier`, repeat each scenario many
rounds, and fail on a timeout instead of hanging. They were run against the lock-free repository first and
failed every time: `Dictionary` corruption exceptions, and the same new id reported `Created` by 3 threads at
once. Through HTTP, parallel POSTs got `500` responses. With the lock, all pass deterministically.

## Frontend architecture

```
frontend/src
  features/simulator/   /add: form, random generator, burst, lifecycle updates
  features/monitor/     /monitor: SignalR hook, reducer, batcher, filter, list
  shared/api/           types, apiClient, ApiError
```

- **Loading order:** the dashboard connects to the SignalR hub first and only then fetches the
  `GET /api/transactions` snapshot, so no event can be missed between the two. After every reconnect the
  snapshot is reloaded, because SignalR does not replay events missed while disconnected.
- **State** lives in a `useReducer` (`transactionsReducer`), with no state library. Rows are kept in a map keyed
  by `transactionId`. The reducer applies the server's rule, "ignore an older timestamp" (compared as instants,
  not strings), which makes merging idempotent: a live event that arrived before the snapshot is not overwritten
  by it, broadcasts arriving in reverse order are harmless, and reloading after a reconnect never creates
  duplicates or rolls rows back.
- **API calls** go through `shared/api/apiClient.ts`, which turns every failure into one `ApiError` with a
  `kind`: `validation` (400, with messages per field), `server` (500) or `network` (backend unreachable,
  including `502`/`504` from the dev proxy). Components never parse HTTP responses themselves.

### Staying responsive under bursts

The assignment requires that 100 transactions arriving quickly do not freeze the browser:

1. **Batching** – SignalR messages are buffered (`createBatcher`) and dispatched to the reducer together at most
   every 100ms, so a burst of 100 messages causes one render instead of 100. Unlike throttling on the server,
   every message is kept; the cost is up to 100ms of extra latency, not noticeable on a dashboard. The batcher is
   plain TypeScript, so a test proves that 100 rapid messages produce a single flush.
2. **Bounded list** – at most 500 rows (the newest) are kept and rendered.
3. **`React.memo` on `TransactionRow`** – unchanged transactions keep the same object in the reducer, so only
   new and updated rows re-render.
4. **No virtualization** (`react-window`) – 500 rows is a small DOM, and virtualization would complicate row
   animations. It would be added only if measurement showed long tasks.

To measure: open DevTools → Performance on the dashboard tab, start recording, press **Send burst of 100** on
the simulator, and check for long tasks.

## Scaling to multiple pods

With several replicas, a client connected to Pod A would not see a transaction posted to Pod B, and each pod
would hold a different in-memory state. The design for solving both is in
**[ADR 0001: Synchronizing state and live updates across pods](docs/adr/0001-multi-pod-sync.md)**:

- **Live updates:** a SignalR Redis backplane, so a broadcast from any pod reaches clients on every pod.
- **State:** a shared implementation of `ITransactionRepository` (SQL or Redis) with an atomic, timestamp-guarded
  upsert, so every pod returns the same snapshot and applies the same rule.

## Security

- No authentication in this MVP.
- The API accepts transaction data via POST.
- In production, authentication and authorization are required.
- Access to data should be restricted by user role.

## Key Decisions

- **Walking skeleton first.** A thin end-to-end slice (form → API → storage → SignalR → dashboard) was built
  before any layer was hardened, so integration risks (proxy, SignalR, JSON field names) surfaced early and
  there was always a demoable system.
- **SignalR, not raw WebSockets.** Automatic reconnect, transport fallback and a thread-safe broadcast API
  (`IHubContext`) out of the box, plus a built-in Redis backplane for running several pods.
- **API contract (DTO) separate from the domain model.** Request fields are nullable, so a missing `amount` is
  not silently read as `0`; the domain `Transaction` stays an immutable, always-valid record; and the external
  contract can change without touching business logic.
- **Validation with DataAnnotations and an enum in the DTO, not a hand-written validator.** Less code, and every
  kind of bad input (missing field, invalid value, broken JSON) produces the same `ValidationProblemDetails`
  format, so the frontend parses a single shape. Validation is tested through HTTP, where it actually runs.
- **A single `lock` in the repository, not `ConcurrentDictionary`** – see [Concurrency](#concurrency).
- **Controller and notifier are covered by integration tests, not mocked unit tests.** Both are thin layers;
  the value is in testing them against the real pipeline (routing, model binding, JSON, SignalR).
- **No CORS.** The frontend and the API are always served from the same origin (the Vite proxy in development,
  the same container in production), so a CORS policy would be code that does nothing. If the frontend were ever served from another domain, `AddCors`
  with the origin from configuration would be added.
- **Frontend organized by feature.** Each route is a self-contained slice with its own components, hooks and
  state; only code both need lives in `shared/`.
- **Client-side batching of live updates** – see [Staying responsive under bursts](#staying-responsive-under-bursts).
- **API types written by hand** in `frontend/src/shared/api/types.ts`. With two endpoints and five fields this is
  simpler than code generation and adds no build step. In a larger system the types would be generated from the
  OpenAPI document, to prevent drift between client and server.

## Known limitations

- **Single instance.** State and SignalR connections live in one process; running several replicas needs shared
  state and a backplane (see [Scaling to multiple pods](#scaling-to-multiple-pods)).
- **In-memory storage.** Restarting the backend loses all transactions. An open dashboard keeps the rows it
  already has, because reloading merges into the current state rather than replacing it.
- **Evicted transactions are forgotten.** Once a transaction has been removed at capacity, its timestamp is no
  longer known, so a stale update for it that arrives later is stored as a new transaction (`Created`).
- **No authentication** (see [Security](#security)).
