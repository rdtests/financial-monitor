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

## Key Decisions

- **Walking skeleton first.** A thin end-to-end slice (form → API → storage → SignalR → dashboard) is built
  before any layer is hardened, so integration risks (proxy, SignalR, JSON field names) surface early and
  there is always a demoable system.
