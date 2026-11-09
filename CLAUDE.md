# Project: Real-Time Financial Monitor (home assignment)

Full requirements: @docs/ASSIGNMENT.md

## Stack
- Backend: .NET 8, ASP.NET Core, SignalR, in-memory storage
- Tests: xUnit, Shouldly, NSubstitute
- Frontend: React + TypeScript (Vite), React Router

## Key decisions
- amount is decimal, never double
- transactionId is the key: same id = update (status lifecycle Pending -> Completed/Failed)
- Ignore updates with an older timestamp than the stored one
- Layers: Controller -> Service -> Repository / Notifier, behind interfaces with DI

## Working rules
- Strict TDD: write a failing test first, then the minimal implementation
- Work in small steps; stop after each phase so I can review and commit
- Do not run git commit or git push; I commit myself
- Update README.md in every step, as part of the same change as the code (how to run what was added, and which decision was made and why)
- Prefer simple, readable code over clever code, and explain non-obvious choices