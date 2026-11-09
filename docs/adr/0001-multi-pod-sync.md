# ADR 0001: Synchronizing state and live updates across pods

- **Status:** Accepted
- **Date:** 2026-11-10

## Context

The MVP runs as a single process. Two things live inside that process:

1. **SignalR connections.** Each dashboard holds a WebSocket to one pod. A broadcast reaches only the clients
   connected to the pod that sends it.
2. **Transaction state.** `InMemoryTransactionRepository` keeps the latest transactions in that pod's memory.

When the backend is scaled to several replicas behind a Kubernetes Service, requests are load-balanced across
pods, and both break:

```
                      ┌──────── Pod A ────────┐
 Dashboard 1 ──WS──►  │ connections: {1}      │
                      │ repository: {…}       │
                      └───────────────────────┘
                      ┌──────── Pod B ────────┐
 POST /transactions ► │ connections: {2}      │  ──► broadcast reaches Dashboard 2 only
 Dashboard 2 ──WS──►  │ repository: {…, new}  │
                      └───────────────────────┘
```

- **Broadcast gap:** a POST handled by Pod B is pushed only to clients connected to Pod B. Dashboard 1 never sees
  it. This is the problem the assignment describes.
- **State gap:** each pod has its own repository, so `GET /api/transactions` returns a different snapshot
  depending on which pod answers, and the "ignore an older timestamp" rule is enforced per pod only. An update
  for the same id can be accepted by one pod and rejected by another.

These are two separate problems, and fixing only the first leaves the second.

## Decision

Solve each problem with its own layer, both behind abstractions the code already has.

### 1. Broadcast: SignalR Redis backplane

Register `AddStackExchangeRedis(...)` on the SignalR builder. Every pod publishes its hub messages to Redis
Pub/Sub, and every pod forwards the messages it receives to its own connected clients. A POST on Pod B then
reaches Dashboard 1 on Pod A.

- **Code change:** one line in `Program.cs`, enabled only when `Redis:ConnectionString` is configured. Without
  it, the app keeps working in memory, so local development and the tests need no Redis.
- `SignalRTransactionNotifier` and `IHubContext` do not change; the backplane sits underneath them.

### 2. State: a shared repository

Replace `InMemoryTransactionRepository` with another implementation of the same `ITransactionRepository`,
backed by a store all pods share. The upsert must stay atomic across pods, so the timestamp rule is enforced in
the store, not in application memory:

- **SQL:** a conditional upsert, e.g. `UPDATE … SET … WHERE TransactionId = @id AND Timestamp <= @timestamp`,
  falling back to `INSERT` when the id does not exist (or a single `MERGE`). The affected-row count gives back
  `Created` / `Updated` / `IgnoredStale`. The latest-N snapshot is an indexed `ORDER BY Timestamp DESC`.
- **Redis:** a Lua script that compares the stored timestamp and writes in one atomic step, plus a sorted set
  scored by timestamp for the snapshot and for evicting the oldest entries.

The service, controller and notifier do not change: this is exactly why the repository sits behind an
interface. The in-process `lock` remains correct for the single-instance in-memory implementation.

### Why the existing design already fits

- Broadcasts from different pods can reach a client in any order. The dashboard already applies the same
  "ignore an older timestamp" rule in its reducer, so merging stays idempotent.
- When a pod dies, its clients reconnect to another pod. The dashboard already reloads the snapshot after every
  reconnect; with a shared repository that snapshot is consistent no matter which pod serves it.

## Alternatives considered

| Option | Why not chosen (for now) |
|--------|--------------------------|
| **Azure SignalR Service** | Moves connection handling out of the pods entirely and scales furthest with the least operational work. Rejected for now because it ties the system to one cloud provider and adds cost; it is the natural next step if the number of connections grows. Switching is a configuration change. |
| **Kafka / RabbitMQ** between pods | Durable messaging and replay, but a heavy component to run for fan-out of small UI events. Worth it only if other services consume the transaction stream. |
| **Sticky sessions only** | Pins a client to one pod but does not deliver Pod B's events to Pod A's clients, and does not unify state. It does not solve the problem. |
| **Backplane only, keep per-pod memory** | Fixes live updates but leaves inconsistent snapshots and a per-pod timestamp rule. Acceptable only as an intermediate step. |

## Consequences

**Positive**

- Any pod can serve any request; pods can be added or removed freely.
- Small code change, because the seams already exist (`ITransactionRepository`, SignalR's backplane hook).
- Local development stays dependency-free (no Redis unless configured).

**Negative / to manage**

- Redis (and a database, for the shared repository) become infrastructure to run, secure and monitor.
- If Redis is unavailable, broadcasts between pods stop. Requests still succeed (a failed broadcast is logged, not
  returned as an error), and dashboards catch up from the snapshot when they reconnect.
- A Redis Pub/Sub backplane does not buffer: a client that is disconnected during a broadcast misses it. The
  snapshot reload after reconnect covers this.

## Deployment notes

- **No sticky sessions needed** when clients use WebSockets only (`skipNegotiation: true` with the WebSockets
  transport). With negotiation and fallback transports (long polling), the negotiate request and the connection
  must reach the same pod, which requires session affinity.
- **Ingress** must allow the WebSocket upgrade and an idle timeout longer than SignalR's keep-alive interval
  (15s by default), so connections are not cut.
- **Probes:** readiness and liveness use `GET /healthz`.
- **Graceful shutdown:** on termination a pod should stop receiving traffic before it closes connections, so
  clients reconnect to another pod; `terminationGracePeriodSeconds` should cover that.

## Scope in this repository

- **To be implemented:** the Redis backplane, enabled by configuration, with a `docker-compose` setup running
  two API instances and Redis to demonstrate it.
- **Documented here only:** the shared repository. It needs atomic conditional upserts, eviction through a
  sorted set and Redis or a database in the tests and CI, a large cost for the part the assignment marks as
  optional.
