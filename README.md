# CodeJudge

Backend for a mini code-submission & evaluation platform: submit a solution, it is evaluated
asynchronously against a three-item rubric (Security → Compiles → Test), and the status and results
are exposed over a versioned REST API.

.NET 10 · ASP.NET Core (controllers, API versioning) · EF Core 10 + Npgsql · PostgreSQL 17 ·
Clean Architecture.

## Design docs

The authoritative design lives in [`docs/`](docs/):

- [`docs/System_Design_v1.md`](docs/System_Design_v1.md)
- [`docs/Database_Design_v1.md`](docs/Database_Design_v1.md)

## Status

Phase 1 skeleton. See the repo's implementation notes for what is and isn't wired up yet
(the Python evaluator is functional end-to-end; C#/JS execution and several extras are still to come).
A full README will follow.

## Build & test

```bash
dotnet build
dotnet test
```

Running the API requires PostgreSQL; the connection string and API key are in
`src/CodeJudge.Api/appsettings.json`.
