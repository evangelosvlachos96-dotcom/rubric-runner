# RubricRunner (solution: `CodeJudge`)

Backend for a mini code-submission and evaluation platform. Users submit a solution in **C#, Python or JavaScript**; the API accepts it instantly and a background worker grades it against a three-item rubric — **Security → Compiles → Test** — using a small problem catalog with multiple test cases per problem. Status and per-rubric results are exposed over a versioned REST API. Built with **.NET 10**, **Clean Architecture** (deliberately without CQRS/MediatR), **EF Core 10 + PostgreSQL**, and the **database-as-queue** pattern for reliable async processing.

> **Status:** design complete; implementation functional end to end. See [PROJECT_STATUS.md](PROJECT_STATUS.md) for milestones, known limitations and remaining work. Full design in [`docs/`](docs/).

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
- **Ordered, short-circuiting rubric** — `Security` (restricted-keyword scan) runs before anything is compiled; `Compiles` before anything is executed; `Test` runs every catalog case for the problem. A failure skips the rest, but the client always receives exactly three results.
- **Problem catalog + language-agnostic harness** — four problems with per-language signatures and 4–5 test cases each. Test cases go to a tiny harness as JSON on stdin; per-case results come back as JSON on stdout. Adding a problem is one catalog entry; adding a language is one evaluator plus one harness script.
- **Clean Architecture, lean** — four projects with compiler-enforced inward dependencies, but no MediatR, CQRS or generic repositories: four endpoints do not justify the ceremony, and the design doc says why.
- **Uniform API contract** — every response is an `ApiResult<T>` envelope with integer `errorCode`s; enums travel as camelCase strings end to end (`pending`, `evaluating`, `completed`, `error`) with no string literals in code.
- **API-key authentication** via a proper `AuthenticationHandler`, swappable for JWT without touching controllers.

---

## Architecture

```
src/
  CodeJudge.Domain/          ← Entities (Submission aggregate + state machine, EvaluationResult), enums, domain exceptions. No NuGet packages.
  CodeJudge.Application/     ← Use-case services, DTOs, FluentValidation validators, ProblemCatalog, EvaluationService (rubric orchestration),
                               abstractions (ISubmissionRepository, IUnitOfWork, ISubmissionClaimer, ICodeEvaluator, IProblemCatalog), CodeJudgeErrorCode.
  CodeJudge.Infrastructure/  ← EF Core (Npgsql, snake_case) DbContext + migrations, SubmissionClaimer (SKIP LOCKED), EvaluationWorker,
                               language evaluators (Roslyn / python / node), harness scripts, RestrictedKeywordPolicy.
  CodeJudge.Api/             ← Controllers/V1, ApiResult<T>, ExceptionMiddleware, ApiKeyAuthenticationHandler, ValidationActionFilter, Swagger.
tests/
  CodeJudge.UnitTests/       ← Domain state machine, EvaluationService rubric, evaluators, problem catalog, validators, architecture rules.
  CodeJudge.IntegrationTests/← Full HTTP flow through WebApplicationFactory with EF InMemory.
docs/                        ← System and database design (v1 as designed, v2 as built), API documentation, architectural note.
```

**Dependency rule:** `Api → Application, Infrastructure`; `Infrastructure → Application`; `Application → Domain`; `Domain → nothing`. `Api` and `Infrastructure` never reference each other; composition happens in `Program.cs` via `AddApplication()` / `AddInfrastructure()`. Enforced by an architecture test (NetArchTest).

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

`Completed` means "the rubric ran" — a submission that does not compile is `completed` with `compiles.passed = false`. `Error` is reserved for system failures (runtime missing, worker crash after `MaxAttempts`). All transitions are domain methods with guards; illegal transitions throw `InvalidStatusTransitionException`.

### Rubric

| Order | Item | Check | On failure |
|---|---|---|---|
| 1 | `security` | Per-language restricted-keyword scan (`Process.Start`, `os.system`, `child_process`, …) | `compiles` and `test` recorded as `skipped` |
| 2 | `compiles` | C#: Roslyn compile with diagnostics · Python: `ast.parse` · JS: `node --check` | `test` recorded as `skipped` |
| 3 | `test` | All catalog cases via harness; `passed` only if every case passes; reports `testsPassed / testsTotal`, per-case `expected` vs `actual`, duration | — |

### Queue and Worker

The `submissions` table is the queue. The worker polls with a single statement (CTE + `UPDATE … RETURNING` + `FOR UPDATE SKIP LOCKED`), so multiple workers never claim the same row, and a crashed worker's rows are re-claimed once `locked_until` passes.

| Setting | Default | Purpose |
|---|---|---|
| `Evaluation:PollingIntervalSeconds` | `2` | Sleep between empty polls |
| `Evaluation:BatchSize` | `5` | Rows claimed per poll |
| `Evaluation:LockDurationSeconds` | `90` | Lock expiry; must exceed compile + run timeouts |
| `Evaluation:CompileTimeoutSeconds` | `10` | Compile / parse step |
| `Evaluation:RunTimeoutSeconds` | `5` | Harness run (all cases, one process) — process tree killed on expiry |
| `Evaluation:MaxAttempts` | `3` | Poison-message protection → `Error` |

---

## Tech Stack

| Component | Technology | Version |
|---|---|---|
| Framework | ASP.NET Core (controllers) | .NET 10 |
| API versioning | `Asp.Versioning.Mvc` + `ApiExplorer` | 8.x |
| ORM | Entity Framework Core + Npgsql + `EFCore.NamingConventions` | 10 |
| Database | PostgreSQL | 16 or 17 |
| Validation | FluentValidation | 11.x |
| Auth | Static API key (`AuthenticationHandler`) | built-in |
| API docs | Swashbuckle (Swagger UI) | 6.x |
| Logging | Serilog (console, request logging) | 8.x |
| C# evaluation | Roslyn (`Microsoft.CodeAnalysis.CSharp`) + collectible `AssemblyLoadContext` | 4.x |
| Python / JS evaluation | `python3` / `node` subprocesses + JSON harness | host runtimes |
| Testing | xUnit, FluentAssertions, NSubstitute, NetArchTest, `Microsoft.AspNetCore.Mvc.Testing`, EF InMemory | — |

---

## Getting Started

### Prerequisites

- **.NET 10 SDK** (`dotnet --version` → `10.0.x`)
- **PostgreSQL 16 or 17** — locally (`winget install PostgreSQL.PostgreSQL.17` on Windows, or the EDB installer) **or** via Docker (see below)
- **Python 3** and **Node.js** on `PATH` (`python --version`, `node --version`) — required by the Python and JavaScript evaluators. C# evaluation needs nothing extra.

### 1. Clone and restore

```bash
git clone https://github.com/evangelosvlachos96-dotcom/rubric-runner.git
cd rubric-runner
dotnet restore
```

### 2. Configure the database

Create a login and database (in `psql` or pgAdmin):

```sql
CREATE USER codejudge WITH PASSWORD 'codejudge';
CREATE DATABASE codejudge OWNER codejudge;
```

Then set the connection string in `src/CodeJudge.Api/appsettings.Development.json` (create the section if absent):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=codejudge;Username=codejudge;Password=codejudge"
  },
  "ApiKeySettings": {
    "Key": "dev-api-key-change-me"
  },
  "Database": {
    "ApplyMigrationsOnStartup": true
  }
}
```

> **Note:** the API key and database credentials are committed for development convenience. Move them to `dotnet user-secrets`, environment variables or a secrets manager before any real deployment.

### 3. Apply migrations

Migrations are applied automatically on startup in Development when `Database:ApplyMigrationsOnStartup` is `true`. To apply them manually:

```bash
dotnet ef database update --project src/CodeJudge.Infrastructure --startup-project src/CodeJudge.Api
```

### 4. Run

```bash
dotnet run --project src/CodeJudge.Api
```

Swagger UI: `https://localhost:{port}/swagger` (the port is printed in the console).

### 5. Authenticate in Swagger

1. Click the **Authorize** button (padlock) at the top of Swagger UI.
2. Paste the value of `ApiKeySettings:Key` (default `dev-api-key-change-me`) and click **Authorize**.
3. All endpoints now send the `X-Api-Key` header automatically.

### 6. Try it

```bash
API=https://localhost:7xxx   # your port
KEY=dev-api-key-change-me

# Submit a Python solution
curl -sk -X POST "$API/api/v1/submissions" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"sum-two-numbers","language":"python","code":"def sum_two(a, b):\n    return a + b"}'

# → 201 { "status": true, "data": { "id": "...", "status": "pending", "results": [] }, ... }

# Poll for the result (a few seconds later)
curl -sk "$API/api/v1/submissions/{id}" -H "X-Api-Key: $KEY"

# → 200 { "data": { "status": "completed", "results": [
#          { "rubricItem": "security", "passed": true },
#          { "rubricItem": "compiles", "passed": true },
#          { "rubricItem": "test", "passed": true, "testsPassed": 4, "testsTotal": 4, "message": "4/4 test cases passed" } ] } }
```

Or run the whole check in one go: `.\scripts\verify-local.ps1 -BaseUrl $API -ApiKey $KEY` (Windows) / `scripts/verify-local.sh` (bash). More examples in `scripts/curl-samples.sh` and the Postman collection `CodeJudge.postman_collection.json` (set the `baseUrl` and `apiKey` collection variables).

---

## Docker

```bash
docker compose up --build
```

Starts `postgres:17` (named volume, health check) and the API (multi-stage image on `mcr.microsoft.com/dotnet/aspnet:10.0` with `python3` and `nodejs` installed for the evaluators). The API listens on `http://localhost:8080`; Swagger at `/swagger`.

| Variable | Default in compose |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=postgres;Port=5432;Database=codejudge;Username=postgres;Password=postgres` |
| `ApiKeySettings__Key` | `dev-api-key-change-me` |
| `Database__ApplyMigrationsOnStartup` | `true` |

---

## Configuration

| Key | Description | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Npgsql connection string | `Host=localhost;Port=5432;Database=codejudge;Username=postgres;Password=postgres` |
| `ApiKeySettings:Key` | Static API key expected in `X-Api-Key` | `dev-api-key-change-me` |
| `Database:ApplyMigrationsOnStartup` | Run `MigrateAsync()` on startup (Development only) | `false` (`true` in Development) |
| `Evaluation:PollingIntervalSeconds` | Worker poll interval | `2` |
| `Evaluation:BatchSize` | Submissions claimed per poll | `5` |
| `Evaluation:LockDurationSeconds` | Claim lock duration | `90` |
| `Evaluation:CompileTimeoutSeconds` | Compile/parse timeout | `10` |
| `Evaluation:RunTimeoutSeconds` | Harness run timeout | `5` |
| `Evaluation:MaxAttempts` | Attempts before `Error` | `3` |
| `Security:RestrictedKeywords:CSharp` | Denied substrings for C# | `Process.Start, System.IO, System.Net, System.Reflection, unsafe, DllImport` |
| `Security:RestrictedKeywords:Python` | Denied substrings for Python | `os.system, subprocess, import os, open(, eval(, exec(, __import__` |
| `Security:RestrictedKeywords:JavaScript` | Denied substrings for JS | `child_process, require('fs'), require('net'), eval(, process.` |
| `Serilog:*` | Log levels and sinks | Console, `Information` |

---

## API Reference

All endpoints are versioned under `/api/v1/` and require the `X-Api-Key` header (except `/health` and Swagger). Full documentation with request/response examples: [`docs/API_Documentation.md`](docs/API_Documentation.md).

Every response uses the `ApiResult<T>` envelope:

```json
// Success
{ "status": true, "description": "🚀 Houston, we don't have a problem", "data": { ... }, "error": null }

// Error
{ "status": false, "description": null, "data": null, "error": { "errorCode": 3001, "description": "Problem 'nope' was not found." } }
```

### Submissions

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/v1/submissions` | Submit code. Returns `201` with `Location` header and `status: "pending"`. |
| GET | `/api/v1/submissions/{id}` | Status and results. `results` is empty until evaluation completes. |

### Users

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/users/{userId}/submissions?page=1&pageSize=20` | Paginated history (`items`, `page`, `pageSize`, `totalCount`, `totalPages`); `pageSize` ≤ 100. |

### Problems

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/v1/problems` | Catalog: id, title, description, difficulty, per-language signatures, sample cases. |

### Health

| Method | Endpoint | Description |
|---|---|---|
| GET | `/health` | Anonymous; PostgreSQL connectivity. |

### Error Codes

| Code | Name | HTTP | When |
|---|---|---|---|
| 0 | Unknown | 500 | Unhandled exception — safe message only, `TraceId` in logs |
| 1000 | ValidationFailed | 400 | FluentValidation failure or domain rule violation |
| 1001 | NotFound | 404 | Generic not found |
| 1002 | Conflict | 409 | Invalid state transition |
| 2001 | SubmissionNotFound | 404 | Unknown submission id |
| 2002 | CodeTooLarge | 400 | Code exceeds 64 KB |
| 3001 | ProblemNotFound | 400 | `problemId` not in catalog |
| 3002 | UnsupportedLanguage | 400 | Language not one of `csharp`, `python`, `javascript` |
| 4001 | Unauthorized | 401 | Missing or invalid `X-Api-Key` |

Enum values on the wire: `language` → `csharp | python | javascript`; `status` → `pending | evaluating | completed | error`; `rubricItem` → `security | compiles | test`.

---

## Problem Catalog

| `problemId` | Difficulty | C# | Python | JavaScript | Cases |
|---|---|---|---|---|---|
| `sum-two-numbers` | Easy | `int Sum(int a, int b)` | `def sum_two(a, b)` | `function sumTwo(a, b)` | 4 |
| `reverse-words` | Easy | `string ReverseWords(string s)` | `def reverse_words(s)` | `function reverseWords(s)` | 4 |
| `balanced-brackets` | Medium | `bool IsBalanced(string s)` | `def is_balanced(s)` | `function isBalanced(s)` | 5 |
| `two-sum` | Easy | `int[] TwoSum(int[] nums, int target)` | `def two_sum(nums, target)` | `function twoSum(nums, target)` | 4 |

C# submissions define the method as `public static` inside a class (any name); Python and JavaScript submissions define a top-level function with the given name.

---

## Testing

```bash
dotnet test                                                  # everything
dotnet test tests/CodeJudge.UnitTests                        # unit
dotnet test tests/CodeJudge.IntegrationTests                 # integration (EF InMemory, no PostgreSQL needed)
dotnet test --collect:"XPlat Code Coverage"                  # coverage
```

**Unit tests** — `Submission` state machine (every legal and illegal transition); `EvaluationService` rubric (compile failure ⇒ `compiles` fails and `test` skipped; correct solution ⇒ all pass; restricted keyword ⇒ `security` fails, both others skipped; partial pass ⇒ `k/N` message) using a fake `ICodeEvaluator`; `CSharpEvaluator` against Roslyn; `ProblemCatalog` self-consistency (a signature for every language, valid JSON expectations); `CreateSubmissionCommandValidator`; NetArchTest dependency rules.

**Integration tests** — `POST` then `GET` returns the pending submission in `ApiResult` shape; `401` without key; `400` with `errorCode 3001` for an unknown problem. The worker is replaced with a no-op claimer so tests are deterministic.

### Known limitations

- **C# executes in-process** (collectible `AssemblyLoadContext` with a timeout). This is not a sandbox: a hostile submission could exhaust memory or call restricted APIs the keyword scan misses. Python and JavaScript run in child processes that are killed on timeout, which is better but also not isolation. The documented next step is a containerised runner per submission.
- **The restricted-keyword check is a rubric item, not a security control** — it is trivially bypassed by string concatenation. It is what the brief asked for and is labelled as such.
- **`userId` is client-asserted** — the static API key has no per-user identity. JWT is the drop-in replacement.
- See [PROJECT_STATUS.md](PROJECT_STATUS.md) for anything unfinished at submission time.

---

## Design Decisions

The design was written before the code and is the authoritative reference: [`docs/System_Design_v1.md`](docs/System_Design_v1.md) (architecture options with pros/cons, use cases, sequence and state diagrams, every decision with alternatives) and [`docs/Database_Design_v1.md`](docs/Database_Design_v1.md) (ER, 3NF, data dictionary, CRUD matrix, claim query, index strategy). The as-built versions — [`docs/System_Design_v2.md`](docs/System_Design_v2.md) and [`docs/Database_Design_v2.md`](docs/Database_Design_v2.md) — record every deviation with its reason. The 150-word summary is in [`docs/ARCHITECTURAL_NOTE.md`](docs/ARCHITECTURAL_NOTE.md).

**What changed while building** (details in the v2 documents):

- Database engine went PostgreSQL → SQL Server (Docker unavailable locally) → **PostgreSQL** once a native install was set up; verified against PostgreSQL 16, design targets 17 (nothing 17-specific is used).
- Evaluation grew from one hard-coded `sum(a,b)` test to a **problem catalog with multiple cases and a JSON harness**, plus `GET /problems`.
- API moved from minimal APIs + RFC 9457 `ProblemDetails` to **controllers + `Asp.Versioning` + `ApiResult<T>` envelope + integer error codes**, matching the author's existing house style.
- `evaluation_results` gained `output jsonb`, `tests_passed`, `tests_total`, `duration_ms`; lock duration raised from 60 s to 90 s.
- Enums end to end (`HasConversion<string>()` in the database, `JsonStringEnumConverter(CamelCase)` on the wire).

---

## Roadmap

- **Sandboxed evaluation** — run each submission in a short-lived container with CPU/memory/network limits; the `ICodeEvaluator` boundary is unchanged.
- **Problems as data** — `problems` / `test_cases` tables and an admin endpoint; `IProblemCatalog` becomes a repository.
- **Completion notification** — SSE endpoint or webhook instead of polling.
- **JWT authentication** — swap `ApiKeyAuthenticationHandler` for JWT Bearer; derive `userId` from `sub`.
- **Scoring and leaderboard** — the first legitimate CQRS read model.
- **Retention** — scheduled purge of old `completed`/`error` rows.
- Anything unticked in [PROJECT_STATUS.md](PROJECT_STATUS.md).

---

*RubricRunner / CodeJudge — September 2026 — Author: Vlachos Evangelos*
