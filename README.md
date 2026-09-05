# rubric-runner (solution: `CodeJudge`)

Backend for a mini code-submission and evaluation platform. Users submit a solution in **C#, Python or JavaScript**; the API accepts it instantly and a background worker grades it against a three-item rubric — **Security → Compiles → Test** — using a small problem catalog with several test cases per problem. Status and per-rubric results are exposed over a versioned REST API. Built with **.NET 10**, **Clean Architecture** (deliberately without CQRS/MediatR), **EF Core 10 + PostgreSQL**, and the **database-as-queue** pattern for reliable asynchronous processing.

> **Status:** design complete; implementation verified end to end. See [PROJECT_STATUS.md](PROJECT_STATUS.md) for milestones, known limitations and remaining work. Full design in [`docs/`](docs/).

**Documentation**

- [`docs/System_Design_v1.md`](docs/System_Design_v1.md) — system design, as designed (options, decisions, diagrams)
- [`docs/Database_Design_v1.md`](docs/Database_Design_v1.md) — database design, as designed (ER, 3NF, data dictionary, claim query, indexes)
- [`docs/System_Design_v2.md`](docs/System_Design_v2.md) — system design, as built (every change from v1 with its reason)
- [`docs/Database_Design_v2.md`](docs/Database_Design_v2.md) — database design, as built
- [`docs/API_Documentation.md`](docs/API_Documentation.md) — every endpoint with real request/response examples and the error-code reference
- [`docs/ARCHITECTURAL_NOTE.md`](docs/ARCHITECTURAL_NOTE.md) — the 150-word architectural note
- [`docs/prompts/phase1-claude-code-prompt.md`](docs/prompts/phase1-claude-code-prompt.md) — the Phase 1 build prompt the skeleton was generated from
- [`PROJECT_STATUS.md`](PROJECT_STATUS.md) — milestones, verification status, deviations from v1

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Docker](#docker)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Problem Catalog](#problem-catalog)
- [Testing](#testing)
- [Design Decisions](#design-decisions)
- [Roadmap](#roadmap)

---

## Overview

Key features:

- **Async by construction** — `POST /submissions` inserts one row with `status = pending` and returns `201` in milliseconds. That row *is* the job: the `EvaluationWorker` claims it with `FOR UPDATE SKIP LOCKED`, so accepting and scheduling a submission is a single atomic write with no outbox or broker.
- **Ordered, short-circuiting rubric** — `security` (restricted-keyword scan) runs before anything is compiled; `compiles` before anything is executed; `test` runs every catalog case for the problem. A failure skips the rest, but the client always receives exactly three results, with the skipped ones flagged.
- **Problem catalog + language-agnostic harness** — four problems with per-language signatures and 4–5 test cases each. Test cases go to a tiny harness as JSON on stdin; per-case results come back as JSON on stdout. Adding a problem is one catalog entry; adding a language is one evaluator plus one harness script.
- **Clean Architecture, lean** — four projects with compiler-enforced inward dependencies, but no MediatR, CQRS or generic repositories: four endpoints do not justify the ceremony, and [System Design §6.1–6.3](docs/System_Design_v1.md) records why.
- **API-key authentication** via a proper `AuthenticationHandler`, swappable for JWT without touching controllers.
- **Uniform API contract** — every response is an `ApiResult<T>` envelope with integer `errorCode`s; enums travel as camelCase strings end to end (`pending`, `evaluating`, `completed`, `error`) with no string literals in code.

---

## Architecture

```
src/
  CodeJudge.Domain/          ← Submission aggregate + state machine, EvaluationResult, enums, domain exceptions. No NuGet packages.
  CodeJudge.Application/     ← Use-case services, DTOs, FluentValidation validators, ProblemCatalog, EvaluationService (rubric
                               orchestration), abstractions (ISubmissionRepository, IUnitOfWork, ISubmissionClaimer, ICodeEvaluator,
                               IProblemCatalog, IRestrictedKeywordPolicy), CodeJudgeErrorCode.
  CodeJudge.Infrastructure/  ← EF Core (Npgsql, snake_case) DbContext + migrations, SubmissionClaimer (SKIP LOCKED), EvaluationWorker,
                               language evaluators (Roslyn / python / node), embedded harness scripts, RestrictedKeywordPolicy.
  CodeJudge.Api/             ← Controllers/V1, ApiResult<T>, ExceptionMiddleware, ApiKeyAuthenticationHandler, ValidationActionFilter, Swagger.
tests/
  CodeJudge.UnitTests/       ← Domain state machine, EvaluationService rubric, CSharpEvaluator, problem catalog, validators, architecture rules.
  CodeJudge.IntegrationTests/← HTTP contract through WebApplicationFactory with EF InMemory.
docs/                        ← System and database design (v1 as designed, v2 as built), API documentation, architectural note, prompts/.
postman/, scripts/           ← Postman collection, curl samples, one-command end-to-end verification.
```

**Dependency rule:** `Api → Application, Infrastructure`; `Infrastructure → Application → Domain`; `Domain → nothing`. `Api` and `Infrastructure` never reference each other; composition happens in `Program.cs` via `AddApplication()` / `AddInfrastructure(IConfiguration)`. Enforced by `DependencyRuleTests` (NetArchTest).

### Submission State Machine

```
              Submission.Create()
                      │
                      ▼
                  ┌────────┐
                  │Pending │
                  └───┬────┘
           Claim()    │
                      ▼
               ┌────────────┐   Claim() after lock expiry
               │ Evaluating │◄──────── (attempt_count++)
               └──┬──────┬──┘
     Complete()   │      │  Fail()  /  attempt_count > MaxAttempts
                  ▼      ▼
            ┌─────────┐ ┌───────┐
            │Completed│ │ Error │
            └─────────┘ └───────┘
             (terminal)  (terminal)
```

`completed` means "the rubric ran" — a submission that does not compile is `completed` with `compiles.passed = false`. `error` is reserved for system failures (runtime missing, worker crash after `MaxAttempts`). All transitions are domain methods with guards; illegal transitions throw `InvalidStatusTransitionException` (`409 / 1002`).

### Rubric

| Order | Item | Check | On failure |
|---|---|---|---|
| 1 | `security` | Per-language restricted-keyword scan (`Process.Start`, `os.system`, `child_process`, …) — nothing is compiled or run first | `compiles` and `test` recorded as `skipped` |
| 2 | `compiles` | C#: Roslyn compile with diagnostics · Python: `ast.parse` · JS: `node --check` | `test` recorded as `skipped` |
| 3 | `test` | All catalog cases through the harness; `passed` only if every case passes; reports `testsPassed / testsTotal`, per-case `expected` vs `actual`, duration | — |

### Queue and Worker

The `submissions` table is the queue. The worker polls with a single statement (CTE + `UPDATE … RETURNING` + `FOR UPDATE SKIP LOCKED`), so multiple workers never claim the same row, and a crashed worker's rows are re-claimed once `locked_until` passes.

| Setting | Default | Purpose |
|---|---|---|
| `Evaluation:PollingIntervalSeconds` | `2` | Sleep between empty polls |
| `Evaluation:BatchSize` | `5` | Rows claimed per poll |
| `Evaluation:LockDurationSeconds` | `90` | Lock expiry; must exceed compile + run timeouts |
| `Evaluation:CompileTimeoutSeconds` | `10` | Compile / parse step |
| `Evaluation:RunTimeoutSeconds` | `5` | Harness run (all cases, one process) — process tree killed on expiry |
| `Evaluation:MaxAttempts` | `3` | Poison-message protection → `error` |

---

## Tech Stack

| Component | Technology | Version |
|---|---|---|
| Framework | ASP.NET Core (controllers) | .NET 10 / C# 14 |
| API versioning | `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` | 10.2.1 |
| ORM | Entity Framework Core + `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` | 10.0.8 / 10.0.3 / 10.0.1 |
| Database | PostgreSQL | 17 (design & compose); verified locally on 18.6 |
| Validation | FluentValidation | 12.1.1 |
| Auth | Static API key (`AuthenticationHandler<ApiKeyOptions>`) | built-in |
| API docs | Swashbuckle.AspNetCore (Swagger UI) | 8.1.4 |
| Logging | Serilog.AspNetCore (console sink, request logging) | 10.0.0 |
| C# evaluation | Roslyn (`Microsoft.CodeAnalysis.CSharp`) + collectible `AssemblyLoadContext` | 5.0.0 |
| Python / JS evaluation | `python` / `node` subprocesses + JSON harness | host runtimes (verified with Python 3.14, Node 24) |
| Testing | xUnit 2.9.3, FluentAssertions 7.2.2, NSubstitute 6.2.0, NetArchTest.Rules 1.3.2, `Microsoft.AspNetCore.Mvc.Testing`, EF InMemory | — |

---

## Getting Started

### Prerequisites

- **.NET 10 SDK** (`dotnet --version` → `10.0.x`; pinned by `global.json`)
- **PostgreSQL 17** — locally (`winget install PostgreSQL.PostgreSQL.17` on Windows, or the EDB installer) **or** via Docker (see [Docker](#docker)). Any recent version works; nothing 17-specific is used.
- **Python 3** and **Node.js** on `PATH` (`python --version`, `node --version`) — required by the Python and JavaScript evaluators. C# evaluation needs nothing extra.

### 1. Clone and restore

```bash
git clone https://github.com/evangelosvlachos96-dotcom/rubric-runner.git
cd rubric-runner
dotnet restore
```

### 2. Configure the database

Create a dedicated login and database (in `psql` or pgAdmin, connected as the superuser):

```sql
CREATE USER codejudge WITH PASSWORD 'codejudge';
CREATE DATABASE codejudge OWNER codejudge;
```

`src/CodeJudge.Api/appsettings.Development.json` carries the development connection string and turns on start-up migrations. Adjust `Port` to your install (the EDB installer uses `5432` for the first instance, `5433` for a second one):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=codejudge;Username=codejudge;Password=codejudge"
  },
  "Database": {
    "ApplyMigrationsOnStartup": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Information",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
    }
  }
}
```

The API key (`ApiKeySettings:Key`, default `dev-api-key-change-me`) lives in `appsettings.json`.

> **Note:** the API key and database credentials are committed for development convenience only. Move them to `dotnet user-secrets`, environment variables or a secrets manager before any real deployment.

### 3. Apply migrations

In Development, migrations run automatically on start-up when `Database:ApplyMigrationsOnStartup` is `true`. To apply them manually (needs `dotnet tool install -g dotnet-ef`):

```bash
dotnet ef database update --project src/CodeJudge.Infrastructure --startup-project src/CodeJudge.Api
```

### 4. Run

```bash
dotnet run --project src/CodeJudge.Api
```

The API listens on **`http://localhost:5000`** (Kestrel default; override with `ASPNETCORE_URLS`). Swagger UI: <http://localhost:5000/swagger>.

### 5. Authenticate in Swagger

1. Open <http://localhost:5000/swagger> and click the **Authorize** button (padlock) at the top right.
2. In the **ApiKey** box paste the value of `ApiKeySettings:Key` — `dev-api-key-change-me` by default — then **Authorize** and **Close**.
3. Every request now carries the `X-Api-Key` header; expand `POST /api/v1/submissions`, **Try it out**, **Execute**.

### 6. Try it

```bash
API=http://localhost:5000
KEY=dev-api-key-change-me

# Submit a Python solution
curl -s -X POST "$API/api/v1/submissions" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"sum-two-numbers","language":"python","code":"def sum_two(a, b):\n    return a + b"}'
# → 201, Location: /api/v1/submissions/{id}
#   { "status": true, "data": { "id": "…", "status": "pending", "results": [] }, … }

# Poll for the result (a few seconds later)
curl -s "$API/api/v1/submissions/{id}" -H "X-Api-Key: $KEY"
# → 200 { "data": { "status": "completed", "results": [
#          { "rubricItem": "security", "passed": true,  "skipped": false },
#          { "rubricItem": "compiles", "passed": true,  "skipped": false, "message": "Compiled successfully." },
#          { "rubricItem": "test",     "passed": true,  "skipped": false, "message": "4/4 test cases passed",
#            "testsPassed": 4, "testsTotal": 4, "output": { "cases": [ … ] } } ] } }
```

Or run the whole acceptance check (health, three languages to `completed 4/4`, security short-circuit, every error code, catalog, user history) in one command against a running API:

```powershell
.\scripts\verify-local.ps1 -BaseUrl http://localhost:5000 -ApiKey dev-api-key-change-me   # Windows
```
```bash
scripts/verify-local.sh http://localhost:5000 dev-api-key-change-me                        # bash
```

More examples: [`scripts/curl-samples.sh`](scripts/curl-samples.sh) and the Postman collection [`postman/CodeJudge.postman_collection.json`](postman/CodeJudge.postman_collection.json) (set the `baseUrl` and `apiKey` collection variables; the POST request stores the returned id in `submissionId` for the GET).

---

## Docker

```bash
docker compose up --build
```

Starts `postgres:17` (named volume `codejudge-pgdata`, `pg_isready` health check) and the API (multi-stage image: `mcr.microsoft.com/dotnet/sdk:10.0` build → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime with `python3` and `nodejs` installed for the evaluators, running as a non-root user). The API waits for PostgreSQL to be healthy, applies migrations, and listens on **`http://localhost:8080`** (Swagger at `/swagger`).

| Variable | Default in compose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;Port=5432;Database=codejudge;Username=postgres;Password=postgres` |
| `ApiKeySettings__Key` | `dev-api-key-change-me` |
| `Database__ApplyMigrationsOnStartup` | `true` |

Ports: `8080` (API) and `5432` (PostgreSQL, published for `psql`/pgAdmin access).

---

## Configuration

Every key can be set in `appsettings*.json` or as an environment variable (`Section__Key`).

| Key | Description | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Npgsql connection string | `Host=localhost;Port=5432;Database=codejudge;Username=postgres;Password=postgres` |
| `ApiKeySettings:Key` | Static API key expected in `X-Api-Key` | `dev-api-key-change-me` |
| `Database:ApplyMigrationsOnStartup` | Run `MigrateAsync()` on start-up (Development only; skipped for non-relational providers) | `false` (`true` in Development) |
| `Evaluation:PollingIntervalSeconds` | Worker poll interval when the queue is empty | `2` |
| `Evaluation:BatchSize` | Submissions claimed per poll | `5` |
| `Evaluation:LockDurationSeconds` | Claim lock duration | `90` |
| `Evaluation:CompileTimeoutSeconds` | Compile / parse timeout | `10` |
| `Evaluation:RunTimeoutSeconds` | Harness run timeout (all cases) | `5` |
| `Evaluation:MaxAttempts` | Claims before the submission is marked `error` | `3` |
| `Security:RestrictedKeywords:CSharp` | Denied substrings for C# | `Process.Start`, `System.IO`, `System.Net`, `System.Reflection`, `unsafe`, `DllImport` |
| `Security:RestrictedKeywords:Python` | Denied substrings for Python | `os.system`, `subprocess`, `import os`, `open(`, `eval(`, `exec(`, `__import__` |
| `Security:RestrictedKeywords:JavaScript` | Denied substrings for JavaScript | `child_process`, `require('fs')`, `require('net')`, `eval(`, `process.` |
| `Serilog:*` | Minimum levels and sinks | Console, `Information` (`Warning` for ASP.NET Core / EF Core) |
| `AllowedHosts` | Host filtering | `*` |

---

## API Reference

All endpoints are versioned under `/api/v1/` and require the `X-Api-Key` header, except `/health` and `/swagger`. Full documentation with request/response examples: [`docs/API_Documentation.md`](docs/API_Documentation.md).

Every response uses the `ApiResult<T>` envelope:

```json
// Success
{ "status": true, "description": "🚀 Houston, we don't have a problem", "data": { … }, "error": null }

// Error
{ "status": false, "description": "Problem 'nope' does not exist.", "data": null,
  "error": { "errorCode": 3001, "description": "Problem 'nope' does not exist." } }
```

### Submissions

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/submissions` | key | Submit code. `201` with `Location: /api/v1/submissions/{id}` and `status: "pending"`. |
| GET | `/api/v1/submissions/{id}` | key | Status and rubric results. `results` is empty until evaluation completes, then has exactly three items. |

### Users

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/v1/users/{userId}/submissions?page=1&pageSize=20` | key | Paginated history, newest first: `items`, `page`, `pageSize`, `totalCount`, `totalPages`. `page ≥ 1`, `1 ≤ pageSize ≤ 100`. |

### Problems

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/v1/problems` | key | Catalog: id, title, description, difficulty, per-language signatures, sample cases. |

### Health

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/health` | none | PostgreSQL connectivity; plain-text `Healthy` / `503`. |

### Error codes ([System Design §6.10](docs/System_Design_v1.md))

| Code | Name | HTTP | When |
|---|---|---|---|
| 0 | Unknown | 500 | Unhandled exception — safe message only, `TraceId` in logs |
| 1000 | ValidationFailed | 400 | FluentValidation failure, invalid paging, malformed body, domain rule violation |
| 1001 | NotFound | 404 | Generic not found |
| 1002 | Conflict | 409 | Invalid state transition |
| 2001 | SubmissionNotFound | 404 | Unknown submission id |
| 2002 | CodeTooLarge | 400 | `code` exceeds 65 536 characters |
| 3001 | ProblemNotFound | 400 | `problemId` not in the catalog |
| 3002 | UnsupportedLanguage | 400 | `language` is not one of the supported values |
| 4001 | Unauthorized | 401 | Missing or invalid `X-Api-Key` |

**Enum values on the wire** (camelCase of the C# enum names; input is case-insensitive):

| Field | Values |
|---|---|
| `language` | `cSharp`, `python`, `javaScript` (send `csharp` / `javascript` if you prefer — accepted) |
| `status` | `pending`, `evaluating`, `completed`, `error` |
| `rubricItem` | `security`, `compiles`, `test` |

---

## Problem Catalog

| `problemId` | Title | Difficulty | C# | Python | JavaScript | Cases |
|---|---|---|---|---|---|---|
| `sum-two-numbers` | Sum Two Numbers | Easy | `int Sum(int a, int b)` | `def sum_two(a, b)` | `function sumTwo(a, b)` | 4 |
| `reverse-words` | Reverse Words | Easy | `string ReverseWords(string s)` | `def reverse_words(s)` | `function reverseWords(s)` | 4 |
| `balanced-brackets` | Balanced Brackets | Medium | `bool IsBalanced(string s)` | `def is_balanced(s)` | `function isBalanced(s)` | 5 |
| `two-sum` | Two Sum | Easy | `int[] TwoSum(int[] nums, int target)` | `def two_sum(nums, target)` | `function twoSum(nums, target)` | 4 |

Python and JavaScript submissions define a top-level function with the given name. C# submissions may be either a bare method (`int Sum(int a, int b) => a + b;` — it is wrapped in a generated `Solution` class, with `System`, `System.Collections.Generic`, `System.Linq` and `System.Text` imported implicitly) or a complete class containing the method.

---

## Testing

```bash
dotnet test                                                  # everything (59 tests, no database or runtime required)
dotnet test tests/CodeJudge.UnitTests                        # unit (53)
dotnet test tests/CodeJudge.IntegrationTests                 # integration (6) — EF InMemory, worker inert
dotnet test --collect:"XPlat Code Coverage"                  # coverage (coverlet)
```

**Unit tests** — `SubmissionStateMachineTests`: every legal and illegal transition, factory guards. `EvaluationServiceTests` (fake `ICodeEvaluator`, no runtimes): compile failure ⇒ `compiles` fails and `test` skipped; correct solution ⇒ all three pass; restricted keyword ⇒ `security` fails and both others skipped; partial pass ⇒ `k/N test cases passed`; timeout; harness crash; system errors surface as exceptions. `CSharpEvaluatorTests` (real Roslyn): diagnostics with correct user line numbers, bare-method wrapping, wrong answers, runtime exceptions, missing function, infinite loop timeout. `ProblemCatalogTests`: a signature for every language, valid self-contained JSON expectations, unique case ids. `CreateSubmissionCommandValidatorTests`. `DependencyRuleTests` (NetArchTest): Domain depends on nothing, Application never on Infrastructure/Api/EF, Infrastructure never on Api.

**Integration tests** — `POST` then `GET` returns the pending submission in `ApiResult` shape; `401/4001` without a key; `400/3001` for an unknown problem; `400/3002` for an unknown language; `404/2001` for an unknown id; the `Location` header path. The worker is replaced with a no-op claimer so tests are deterministic.

**End-to-end** — `scripts/verify-local.{sh,ps1}` runs the acceptance flow against a live API + PostgreSQL (see [Getting Started](#6-try-it)).

### Known limitations

- **C# executes in-process** in a collectible `AssemblyLoadContext` with a timeout. This is not a sandbox: a hostile submission can exhaust memory, and a timed-out thread is abandoned rather than killed (its context is unloaded only once the thread ends). Submissions compile against a fixed allow-list of BCL assemblies, which limits — but does not prevent — access to APIs the keyword scan misses.
- **Python and JavaScript run in child processes** killed on timeout (whole process tree). Better than in-process, but still not isolation; no CPU, memory or network limits are enforced.
- **The restricted-keyword check is a rubric item, not a security control** — it is trivially bypassed by string concatenation. It is what the brief asked for and is labelled as such.
- **`userId` is client-asserted** — the static API key has no per-user identity. JWT is the drop-in replacement.
- **`docker build` was not run** on the development machine (Docker Desktop daemon unavailable); the Dockerfile and compose file were validated with `docker compose config` only.

---

## Design Decisions

The design was written before the code and is the authoritative reference: [`docs/System_Design_v1.md`](docs/System_Design_v1.md) (architecture options with pros/cons, use cases, sequence and state diagrams, every decision with its alternatives) and [`docs/Database_Design_v1.md`](docs/Database_Design_v1.md) (ER, 3NF, data dictionary, CRUD matrix, claim query, index strategy). The as-built versions — [`docs/System_Design_v2.md`](docs/System_Design_v2.md) and [`docs/Database_Design_v2.md`](docs/Database_Design_v2.md) — record every deviation with its reason. The 150-word summary is [`docs/ARCHITECTURAL_NOTE.md`](docs/ARCHITECTURAL_NOTE.md).

**What changed while building** — the full table with reasons is in [PROJECT_STATUS.md](PROJECT_STATUS.md#deviations-from-v1-design):

| # | Area | v1 design | As built |
|---|---|---|---|
| 1 | `ICodeEvaluator.RunAsync` | returns `IReadOnlyList<TestCaseResult>` | returns `HarnessRunResult` (cases + `TimedOut`, `Stderr`, `DurationMs`) |
| 2 | Timeouts | one `Evaluation:TimeoutSeconds` | `CompileTimeoutSeconds` (10) + `RunTimeoutSeconds` (5) |
| 3 | Validator name | `CreateSubmissionRequestValidator` | `CreateSubmissionCommandValidator` (validates the Application command) |
| 4 | `EvaluationResult` factories | `Passed` / `Failed` / `SkippedResult` | `Pass` / `Fail` / `Skip` (`Passed` collides with the data property) |
| 5 | Start-up migrations | not configurable | `Database:ApplyMigrationsOnStartup`, skipped for non-relational providers |
| 6 | C# harness | generated harness class compiled with the user code | harness loop runs host-side by reflection over the loaded assembly |
| 7 | C# submission shape | signature only | bare method (wrapped) **or** full class; implicit global usings |
| 8 | C# timeout | context unloaded on timeout | timeout reported, runaway thread abandoned, unload best-effort |
| 9 | C# compile references | unspecified | fixed allow-list of BCL assemblies |
| 10 | Local database | PostgreSQL 17 on 5432, `postgres`/`postgres` | verified on PostgreSQL 18.6 on 5433 with a dedicated `codejudge` login |
| 11 | Binding errors | not specified | `ModelState` failures → `ValidationException`; unknown `language` → `400/3002`, other → `400/1000` |
| 12 | Lock duration | SD §4.1/§7 say 60 s, DB §8 says 90 s | 90 s (`Evaluation:LockDurationSeconds`), per the database design |

---

## Roadmap

From [PROJECT_STATUS.md](PROJECT_STATUS.md):

- **Run `docker build` / `docker compose up`** on a machine with a working Docker daemon.

From [System Design §11](docs/System_Design_v1.md):

- **Sandboxed evaluation** — run each submission in a short-lived container (or microVM) with CPU/memory/network limits; the `ICodeEvaluator` boundary is unchanged.
- **Problems as data** — `problems` / `test_cases` tables and an admin endpoint; `IProblemCatalog` becomes a repository.
- **Completion notification** — SSE endpoint or webhook instead of polling.
- **JWT authentication** — swap `ApiKeyAuthenticationHandler` for JWT Bearer; derive `userId` from `sub`.
- **Scoring and leaderboard** — the first legitimate CQRS read model.
- **Separate evaluator service** — replace DB polling with a broker + outbox when scale demands it.
- **Load testing** — k6 script measuring submissions/s vs. poll latency.

---

*rubric-runner / CodeJudge — September 2026 — Author: Vlachos Evangelos*
