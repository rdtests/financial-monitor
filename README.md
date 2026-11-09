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
| `POST /api/transactions` | Ingest a transaction. `201` with `{ transactionId, outcome: "Created" }` for a new id, `200` with `outcome: "Updated"` for an existing one, `200` with `outcome: "IgnoredStale"` for an out-of-order update, `400` for invalid input (see [Validation](#validation)) |
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

**Transaction Simulator (`/add`)** plays the external system:

- **Send** – a new transaction from the form. The form does no client-side validation on purpose (`noValidate`):
  the server is the source of truth, so its `400` errors appear next to each field (try an empty amount or `0`).
- **Send random** – a random transaction (mostly `Pending`).
- **Send burst of 100** – 100 random transactions in parallel, for checking that the dashboard stays responsive.
  The result reports how many succeeded and failed (`Promise.allSettled`).
- **Last sent** – follow-up updates for the last accepted transaction (same `transactionId`): **Complete** /
  **Fail** walk the lifecycle (`Pending → Completed / Failed`), and **Stale update** sends an older timestamp,
  which the server answers with `IgnoredStale` (shown as information, not an error).

**Live Dashboard (`/monitor`):**

- **Status badges** – Completed green, Failed red, Pending orange, each with an icon and text so the status
  never relies on color alone.
- **Filter** – All / Pending / Completed / **Errors only** (Failed), purely client-side.
- **Connection status** – `Live`, or `Reconnecting…` while the connection is down. Reconnect retries forever
  (1s, 2s, 4s, 8s, then every 10s); SignalR's default gives up after 4 attempts, which would leave a dashboard
  that stays open all day silently disconnected. There is therefore no Reconnect button.
- **After a reconnect** the snapshot is reloaded in the background, because SignalR does not replay events
  missed while disconnected. The reducer ignores older data, so this never creates duplicates or rolls rows back.
- **Snapshot load failure** – a banner with a Retry button; live events keep arriving meanwhile.

**How the dashboard loads:** it connects to the SignalR hub first and only then fetches the
`GET /api/transactions` snapshot, so no event can be missed between the two.

**Dashboard state** lives in a `useReducer` (`transactionsReducer`), with no state library. Rows are kept in a
map keyed by `transactionId`, so an update replaces its row. The reducer applies the same rule as the server,
"ignore an older timestamp" (compared as instants, not strings), which makes merging idempotent: a live event
that arrived before the snapshot is not overwritten by it, and broadcasts arriving in reverse order are harmless.
At most 500 rows are kept in the browser (the newest).

**API calls** go through `shared/api/apiClient.ts`, which turns every failure into one `ApiError` with a `kind`:
`validation` (400, with messages per field), `server` (500) or `network` (backend unreachable, including
`502`/`504` from the dev proxy). Components never parse HTTP responses themselves.

### Staying responsive under bursts

The assignment requires that 100 transactions arriving quickly do not freeze the browser:

1. **Batching** – SignalR messages are buffered (`createBatcher`) and dispatched to the reducer together at most
   every 100ms, so a burst of 100 messages causes one render instead of 100. The batcher is plain TypeScript,
   so a test proves that 100 rapid messages produce a single flush.
2. **Bounded list** – at most 500 rows are kept and rendered.
3. **`React.memo` on `TransactionRow`** – unchanged transactions keep the same object in the reducer, so only
   new and updated rows re-render.
4. **No virtualization** (`react-window`) – 500 rows is a small DOM, and virtualization would complicate the row
   animations. It would be added only if measurement showed long tasks.

To measure: open DevTools → Performance, start recording, press **Send burst of 100** on the simulator, and
check the dashboard tab for long tasks.

## Tests

```bash
cd backend
dotnet test

cd frontend
npm test
```

Backend: xUnit + Shouldly + NSubstitute, including integration tests with `WebApplicationFactory`.
Frontend: Vitest, for pure logic only (reducer, API client, filter, batcher): the parts where bugs hide, without the cost of
component tests.

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

### Update rules

- **Older timestamp is ignored.** The external system may deliver updates out of order. An update whose
  `timestamp` is older than the stored one is not saved and not broadcast; the API answers `200` with
  `outcome: "IgnoredStale"` (it is expected behavior, not an error).
- **Equal timestamp is accepted** as an update, so a `Pending → Completed` change within the same second is not
  lost and a retried request does not fail. Different transactions may share a timestamp.
- **No status lifecycle is enforced.** The timestamp is the only rule, so `Failed → Completed` (e.g. a successful
  retry) is accepted if it is newer. The external system is the source of truth; this is a monitor.
- **Capacity.** The server keeps the latest 1000 transactions (`Transactions:Capacity` in `appsettings.json`).
  When a new transaction arrives at capacity, the one with the oldest timestamp is removed. Without a bound,
  memory would grow forever.

**Known limitation:** once a transaction has been evicted, its timestamp is forgotten. A stale update for it that
arrives later is stored as a new transaction (`Created`) instead of being ignored. Acceptable for this MVP.

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

### Error handling

Every error response is `ProblemDetails` (RFC 7807), so clients parse one format.

| Code | When |
|------|------|
| `400` | Invalid input (see [Validation](#validation)) |
| `500` | Unexpected exception: a generic `ProblemDetails` with a `traceId` for correlating with the server log. No exception details or stack trace outside Development |

- A single global handler (`AddProblemDetails` + `UseExceptionHandler`) instead of try/catch in controllers.
  In Development, .NET 8 shows the Developer Exception Page with the stack trace instead; that is intended for
  local debugging, and Docker / Kubernetes run as Production.
- **A failed SignalR broadcast does not fail the request.** The transaction is already stored, so the service
  logs a warning and returns success; dashboards catch up when they reload the snapshot.
- A client that disconnects does not affect the request or the other connected clients.
- Deliberately not used: `401`/`403` (no authentication in the MVP), `404` (no get-by-id endpoint),
  `409` (a stale update is expected, not a conflict).

### Concurrency

- **Repository:** a single `lock` guards the dictionary. `Upsert` is check-then-act (read the stored timestamp,
  compare, write, maybe evict), so the whole sequence must be atomic. `GetLatest` copies inside the lock and
  returns a snapshot, so callers never see the internal collection.
- **No I/O under the lock.** The service broadcasts only after `Upsert` returns, so a slow client never blocks writers.
- **Broadcasting:** SignalR's `IHubContext` is thread-safe and manages connections itself; there is no shared
  state of our own on the broadcast side.
- **Out-of-order broadcasts:** two concurrent updates of the same id may be broadcast in reverse order, so the
  dashboard applies the same "ignore older timestamp" rule; merging is idempotent.

**How it is tested:** the concurrency tests start all threads together on a `Barrier`, repeat each scenario many
rounds, and fail on a timeout instead of hanging. They were run against the lock-free repository first and
failed every time: `Dictionary` corruption exceptions, and the same new id reported `Created` by 3 threads at
once. Through HTTP, parallel POSTs got `500` responses. With the lock, all pass deterministically.

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
- **A single `lock` in the repository, not `ConcurrentDictionary`.** The upsert is a check-and-set across several
  steps; a lock makes it atomic and readable, while `AddOrUpdate` may run its delegate more than once and cannot
  cleanly report Created / Updated / IgnoredStale. Contention is short because no I/O happens under the lock.
- **Client-side batching of live updates.** 100 messages = 1 render. Simpler than throttling on the server and
  keeps every message; the cost is up to 100ms of extra latency, which is not noticeable on a dashboard.
- **Frontend organized by feature.** Each route is a self-contained slice (`features/simulator` for `/add`,
  `features/monitor` for `/monitor`) with its own components, hooks and state; only code both need lives in
  `shared/`.
- **API types written by hand** in `frontend/src/shared/api/types.ts`. With two endpoints and five fields this is
  simpler than code generation and adds no build step. In a larger system the types would be generated from the
  OpenAPI document, to prevent drift between client and server.
