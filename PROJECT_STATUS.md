# CodeJudge — Project Status

| | |
|---|---|
| **Snapshot date** | 4 September 2026 |
| **Design** | `docs/System_Design_v1.md`, `docs/Database_Design_v1.md` (as designed) · `docs/System_Design_v2.md`, `docs/Database_Design_v2.md` (as built) |
| **Status** | Functional and verified end to end against a live PostgreSQL 18.6 (`scripts/verify-local.*`, 12/12 checks). All core and stretch requirements implemented. The Docker image build is the one item not executed on the author's machine. |

This file is the honest state of the repository. The design docs are the source of truth; every place the code departs from them is recorded in [Deviations from v1 design](#deviations-from-v1-design) and folded into the v2 documents.

**Legend:** `[x]` done and verified · `[~]` partial (see note) · `[ ]` not done

---

## Assignment brief — requirement by requirement

| Requirement | Status | Where |
|---|---|---|
| POST submission (`userId`, `problemId`, `code`, `language` ∈ C#/Python/JS) → id + `pending` | ✅ | `SubmissionsController.Create` |
| GET status/result by id — status, rubric items, output/errors | ✅ | `SubmissionsController.GetById` |
| Async evaluation: Compiles/Parses · Passes Basic Test · Security Check · per-item pass/fail + messages | ✅ | `EvaluationService`, evaluators, `RestrictedKeywordPolicy` |
| Background processing; API stays responsive | ✅ | `EvaluationWorker` + `SubmissionClaimer` (DB-as-queue, `SKIP LOCKED`) |
| Database (PostgreSQL preferred) with submissions, status, results, timestamps | ✅ | EF Core + Npgsql, migration `InitialCreate` |
| Project organisation: data / API / jobs / evaluation separated | ✅ | 4 projects, `DependencyRuleTests` (NetArchTest) |
| Async patterns throughout | ✅ | `async`/`await`, `CancellationToken` end to end |
| REST conventions, clear errors, Swagger/OpenAPI | ✅ | `ApiResult<T>`, `CodeJudgeErrorCode`, `ExceptionMiddleware`, Swashbuckle |
| API key on all endpoints | ✅ | `ApiKeyAuthenticationHandler` (`X-Api-Key`) |
| ≥ 2 unit tests on evaluation logic (compile failure, correct solution) | ✅ | `EvaluationServiceTests` (+ keyword, partial, timeout, crash), `CSharpEvaluatorTests` |
| *Stretch:* multiple languages | ✅ | C# (Roslyn), Python, JavaScript — all three verified live to `completed`, 4/4 |
| *Stretch:* paginated list per user | ✅ | `UsersController` |
| *Stretch:* logging | ✅ | Serilog; `SubmissionId`/`WorkerId` scopes in the worker |
| *Stretch:* Dockerfile | ✅ files · ⚠️ image build not run | `Dockerfile`, `docker-compose.yml` (validated with `docker compose config`) |
| *Deliverable:* README with build/run/env/tests/DB | ✅ | `README.md` |
| *Deliverable:* REST API docs | ✅ | Swagger + `docs/API_Documentation.md` |
| *Deliverable:* schema / migration scripts | ✅ | `src/CodeJudge.Infrastructure/Persistence/Migrations` |
| *Deliverable:* sample curl / Postman | ✅ | `scripts/curl-samples.sh`, `scripts/verify-local.*`, `postman/CodeJudge.postman_collection.json` |
| *Deliverable:* architectural note ≤ 150 words | ✅ | `docs/ARCHITECTURAL_NOTE.md` (150 words) |

---

## Milestones

### ✅ M1 — Design

- [x] Architecture options with pros/cons; Clean Architecture (lean) selected — *SD §5*
- [x] Use cases, sequence, state machine, failure scenarios — *SD §3, §4, §7, §9*
- [x] Decision tables for jobs, evaluation, security, validation, auth, API style, database — *SD §6*
- [x] Database design: ER, 3NF, data dictionary, CRUD matrix, claim query, indexes, evolution — *DD §2–§11*
- [x] As-built v2 documents with every deviation — `docs/System_Design_v2.md`, `docs/Database_Design_v2.md`

### ✅ M2 — Skeleton & Domain

- [x] `CodeJudge.slnx`, `global.json` (SDK 10.0.x), `Directory.Build.props` (`net10.0`, C# 14, nullable, `TreatWarningsAsErrors`), `Directory.Packages.props` (central package management)
- [x] Four source projects + two test projects with the Clean Architecture reference graph (`Api → Application, Infrastructure`; `Infrastructure → Application → Domain`)
- [x] `Enums/Language.cs`, `SubmissionStatus.cs`, `RubricItem.cs`
- [x] `Entities/Submission.cs` — aggregate root, state machine (`Create`/`Claim`/`Complete`/`Fail`) with guards
- [x] `Entities/EvaluationResult.cs` — `Pass`/`Fail`/`Skip` factories
- [x] `Exceptions/DomainException.cs`, `InvalidStatusTransitionException.cs`, `UnsupportedLanguageException.cs`

### ✅ M3 — Application

- [x] `Common/CodeJudgeErrorCode.cs`, `NotFoundException`, `PagedResult<T>`, `JsonDefaults`
- [x] `Abstractions/` — `ICodeEvaluator`, `IProblemCatalog`, `IRestrictedKeywordPolicy`, `ISubmissionClaimer`, `ISubmissionRepository`, `IUnitOfWork`
- [x] `Problems/ProblemCatalog.cs` — 4 problems, per-language signatures, 17 JSON test cases
- [x] `Evaluation/EvaluationService.cs` — ordered, short-circuiting, always three results; `JsonValueComparer`
- [x] `Submissions/SubmissionService.cs`, DTOs, `SubmissionMapper`, `Validators/CreateSubmissionCommandValidator.cs`, `AddApplication()`

### ✅ M4 — Infrastructure: persistence

- [x] `Persistence/CodeJudgeDbContext.cs` (Npgsql, snake_case, `IUnitOfWork`), entity configurations, check constraints, partial + covering indexes
- [x] `Persistence/Repositories/SubmissionRepository.cs`, `Persistence/SubmissionClaimer.cs` (CTE + `UPDATE … RETURNING … FOR UPDATE SKIP LOCKED`)
- [x] `Persistence/Migrations/InitialCreate`; guarded start-up migration (`Database:ApplyMigrationsOnStartup`)
- [x] `Security/RestrictedKeywordPolicy.cs` from configuration
- [x] `DependencyInjection.cs` — `AddInfrastructure(IConfiguration)`

### ✅ M5 — API

- [x] `Models/Common/ApiResult.cs`, `ApiJson.cs`, `NumericEnumConverter.cs`; `Models/Requests/CreateSubmissionRequest.cs`
- [x] `Controllers/V1/SubmissionsController.cs` (`POST`, `GET /{id}`), `UsersController.cs`, `ProblemsController.cs` — `Asp.Versioning`, XML docs, `ProducesResponseType`
- [x] `Middleware/ExceptionMiddleware.cs` — exception → (status, `CodeJudgeErrorCode`), 4xx warning / 5xx error with `TraceId`
- [x] `Filters/ValidationActionFilter.cs` — FluentValidation + `ModelState` binding failures → `ValidationException` (`SuppressModelStateInvalidFilter = true`)
- [x] `Authentication/ApiKeyAuthenticationHandler.cs`, `ApiKeyOptions.cs`, `ApiKeySettings.cs` — constant-time compare, 401 `ApiResult` 4001
- [x] `Swagger/ConfigureSwaggerOptions.cs`, `ApiKeySecurityOperationFilter.cs` — version-grouped docs, XML comments, `ApiKey` scheme, examples
- [x] `Program.cs` — Serilog, JSON enum converter, versioning, auth, lowercase URLs, Swagger UI, `AddNpgSql` health check, migrations on start-up
- [x] `appsettings.json`, `appsettings.Development.json`

### ✅ M6 — Worker + evaluators + harness

- [x] `Workers/EvaluationSettings.cs` (validated on start), `Workers/EvaluationWorker.cs` (claim, `MaxAttempts`, per-item isolation, log scopes, `stoppingToken`)
- [x] `Evaluators/ProcessRunner.cs` — stdin/stdout/stderr, hard timeout, `Kill(entireProcessTree: true)`
- [x] `Evaluators/Harness/harness.py`, `harness.js` (embedded resources), `HarnessProtocol.cs`
- [x] `Evaluators/PythonEvaluator.cs` — `ast.parse` compile step, `harness.py` run
- [x] `Evaluators/JavaScriptEvaluator.cs` — `node --check` compile step, `harness.js` run *(was a stub until the Step 3 live verification found it)*
- [x] `Evaluators/CSharpEvaluator.cs` — Roslyn compile with diagnostics, bare-method wrapping, collectible `AssemblyLoadContext`, reflection invoke, `Task.WaitAsync` timeout, BCL allow-list

### ✅ M7 — Tests

- [x] `UnitTests/Evaluation/EvaluationServiceTests.cs` — fake `ICodeEvaluator`: compile failure, correct solution (the two required by the brief), restricted keyword, partial pass, timeout, harness crash, system errors
- [x] `UnitTests/Evaluation/CSharpEvaluatorTests.cs` — real Roslyn: diagnostics with user line numbers, wrapping, wrong answer, exception, missing function, infinite-loop timeout
- [x] `UnitTests/Domain/SubmissionStateMachineTests.cs` — every legal and illegal transition, factory guards
- [x] `UnitTests/Problems/ProblemCatalogTests.cs` — signature for every language, valid JSON expectations, unique case ids
- [x] `UnitTests/Validators/CreateSubmissionCommandValidatorTests.cs`
- [x] `UnitTests/Architecture/DependencyRuleTests.cs` — NetArchTest layer rules
- [x] `IntegrationTests/CodeJudgeWebApplicationFactory.cs` (EF InMemory, no-op claimer), `SubmissionFlowTests.cs` — pending flow, 401/4001, 400/3001, 400/3002, 404/2001, `Location` path

### 🔶 M8 — Repo files + final verification

- [x] `Dockerfile` (sdk:10.0 → aspnet:10.0, `python3` + `nodejs`, non-root, port 8080), `docker-compose.yml` (`postgres:17` with named volume + healthcheck, api) — validated with `docker compose config`
- [x] `postman/CodeJudge.postman_collection.json` (every endpoint, `baseUrl`/`apiKey` variables, POST stores `submissionId`), `scripts/curl-samples.sh`
- [x] `scripts/verify-local.sh`, `scripts/verify-local.ps1` — one-command end-to-end check (a–h), parameterised by base URL and API key
- [x] `README.md`, `docs/API_Documentation.md`, `docs/ARCHITECTURAL_NOTE.md`
- [x] `docs/System_Design_v2.md`, `docs/Database_Design_v2.md`
- [x] **End-to-end run against live PostgreSQL** — done on PostgreSQL 18.6 / port 5433 (dedicated `codejudge` login); both scripts pass 12/12
- [ ] **`docker build` / `docker compose up`** — not run: Docker CLI is installed but the Docker Desktop daemon was not running on the dev machine

---

## Verification status

| Check | State |
|---|---|
| `dotnet build` | Succeeds, 0 warnings (`TreatWarningsAsErrors`) |
| `dotnet test` | 53 unit + 6 integration tests pass; no database or external runtime required |
| Python submission end-to-end (`sum-two-numbers` → `completed`, `testsPassed = 4`) | Verified with `scripts/verify-local.sh` / `.ps1` against local PostgreSQL 18.6 (see deviation 10) |
| JavaScript submission end-to-end | Verified by the scripts (`node` v24) |
| C# submission end-to-end | Verified by the scripts; also covered by `CSharpEvaluatorTests` (real Roslyn, no runtime needed) |
| Security short-circuit (`import os` → `security.passed=false`, others skipped) | Verified by the scripts |
| Error shapes 400/3001, 401/4001, 404/2001, 400/3002; `/problems` (4); user history | Verified by the scripts; 3001/3002/4001/2001 also covered by integration tests |
| `201 Location` header | `/api/v1/submissions/{id}` (fixed during verification — was `/api/v1.0/Submissions/{id}`) |
| Swagger | Confirmed from `/swagger/v1/swagger.json`: one `v1` document, summary + response codes on all four actions, `ApiKey` security scheme applied to every operation, enums rendered as camelCase strings, complete example request body |
| `docker build .` | Not run — Docker Desktop daemon unavailable; Dockerfile and compose validated with `docker compose config` only |

---

## Known limitations

- C# submissions execute **in-process** (collectible `AssemblyLoadContext` + `Task.WaitAsync` timeout) — not a sandbox. A timed-out thread is abandoned, not killed; the context unloads only once it ends. Compilation uses a fixed BCL allow-list, which limits but does not prevent access to APIs the keyword scan misses.
- Python/JavaScript run in child processes killed on timeout (whole tree) — better than in-process, still not isolation; no CPU/memory/network limits.
- The restricted-keyword scan is a **rubric item, not a security control**; trivially bypassed by string concatenation.
- `userId` is client-asserted under the static API key; JWT is the drop-in replacement.
- Verified against PostgreSQL 18.6; design and compose target 17 (no version-specific features used).
- Docker image build not executed on the dev machine.

---

## Deviations from v1 design

Every row is a decision that differs from `docs/System_Design_v1.md` (SD) or `docs/Database_Design_v1.md` (DD) as they stand in `docs/`, and is folded into the v2 documents.

| # | Area | v1 design says | Implementation does | Why |
|---|---|---|---|---|
| 1 | `ICodeEvaluator` signature (SD §6.5) | `Task<IReadOnlyList<TestCaseResult>> RunAsync(...)` | `Task<HarnessRunResult> RunAsync(...)` | A bare case list cannot express "the whole run timed out" or "the harness itself crashed with no per-case results". `HarnessRunResult` wraps the cases with `TimedOut`, `Stderr` and `DurationMs`, which `EvaluationService` needs to build the `Test` result. |
| 2 | Evaluation timeout config (SD §10 vs DD §8) | SD lists one key `Evaluation:TimeoutSeconds`; DD lists two | two keys `Evaluation:CompileTimeoutSeconds` (10) and `Evaluation:RunTimeoutSeconds` (5), per DD | Compiling and running have very different cost profiles — a cold Roslyn/`node --check` start is slower than the run it guards. One shared value would either be too tight for compile or too loose for untrusted execution. |
| 3 | Validator name (SD §6.7) | `CreateSubmissionRequestValidator` | `CreateSubmissionCommandValidator` | Validation runs on the Application-layer `CreateSubmissionCommand`, not the API request model, so the rules stay testable without referencing the API project. The action filter maps the request to the command and resolves that validator. |
| 4 | `EvaluationResult` factory names (DD §9) | `Passed` / `Failed` / `SkippedResult` | `Pass` / `Fail` / `Skip` | `Passed` collides with the `bool Passed` data property that the contract requires. The data property keeps the contract name; the factory verbs were shortened. |
| 5 | Start-up migrations (SD §10) | config list has no migration key | added `Database:ApplyMigrationsOnStartup` (default `false`, `true` in Development); skipped for non-relational providers | Lets Development and Docker Compose create the schema on boot without making that the behaviour elsewhere. The provider guard lets the EF InMemory integration tests share `Program.cs`. |
| 6 | C# execution harness (SD §6.5) | "user code + generated harness class compiled together; harness invoked via reflection" | user code alone is compiled; the harness loop (argument binding, invocation, JSON shaping) runs host-side by reflection over the loaded assembly | Keeps submitted code from being compiled against `System.Text.Json` and keeps the harness protocol in one place instead of duplicated in generated C#. The observable contract — per-case `actual`/`error`/`durationMs` — is identical to the Python and JavaScript harnesses. |
| 7 | C# submission shape (SD §6.5) | signature only, e.g. `int Sum(int a, int b)` | a bare method is accepted and wrapped in a generated `public class Solution`; code that already declares a type or namespace is compiled unchanged; `System`, `System.Collections.Generic`, `System.Linq`, `System.Text` are implicit global usings | The signature table implies a method, but a compilation unit cannot contain a bare method. Accepting both shapes keeps the documented signature honest without forcing boilerplate on the submitter. |
| 8 | C# timeout enforcement (SD §6.5) | "on timeout the context is unloaded and the case marked `Timed out`" | the run is reported as timed out and stops, but the runaway thread is abandoned; `AssemblyLoadContext.Unload()` is called and completes only once that thread ends | .NET has no safe thread abort. Already named in SD §11 as the in-process weak spot; recorded here because the unload is not guaranteed, only requested. |
| 9 | C# compile references (SD §6.5, unspecified) | — | submissions compile against a fixed allow-list: `System.Private.CoreLib`, `System.Runtime`, `System.Runtime.Extensions`, `System.Collections`, `System.Linq`, `System.Console`, `System.Text.RegularExpressions` | Referencing the whole trusted-platform set would expose `System.Diagnostics.Process`, `System.IO`, `System.Net`, … at compile time regardless of the keyword check. The list is the minimum the catalog problems need. |
| 10 | Database version / local setup (SD §6.11, §10) | PostgreSQL 17 on `localhost:5432`, `postgres`/`postgres` | verified locally against **PostgreSQL 18.6 on port 5433** with a dedicated `codejudge`/`codejudge` login owning database `codejudge`; `appsettings.Development.json` carries that connection string, `appsettings.json` and `docker-compose.yml` keep the design default (`postgres:17`, 5432) | The dev machine already had PostgreSQL 16 on 5432 and a fresh PostgreSQL 18 on 5433. A least-privilege login was created instead of using the superuser; the superuser password is not stored anywhere in the repo. Nothing in the schema or queries is version-specific (`FOR UPDATE SKIP LOCKED`, partial indexes, `jsonb` all exist since 9.5). |
| 11 | Binding errors → error code (SD §6.7, §6.10) | validation via FluentValidation; `3002 UnsupportedLanguage` raised by "Validator / `UnsupportedLanguageException`" | `ValidationActionFilter` also converts `ModelState` binding failures (malformed JSON, unknown enum value such as `"language": "cobol"`) into `ValidationException`; a failure on `language` carries `UnsupportedLanguage` → `400/3002`, any other → `400/1000` | With `SuppressModelStateInvalidFilter = true` the built-in 400 is gone, so binding failures otherwise reached the action with a null body and became a `500/0`. Routing them through the same exception path keeps "every 4xx is an `ApiResult`" true. |
| 12 | Lock duration (SD §4.1, §7 vs DD §8) | SD says `LockedUntil = now + 60 s`; DD says 90 s | 90 s (`Evaluation:LockDurationSeconds`), per DD | The v1 documents disagree with each other. 90 s covers compile timeout (10 s) + run timeout (5 s) for a full batch with margin; 60 s was the earlier draft value. |
| 13 | Docker verification (SD §5.4, §6.11) | `docker-compose.yml` as the zero-install path | files delivered and `docker compose config` validated; `docker build` not executed | Docker Desktop daemon unavailable on the dev machine. Unverified item, not a design change; listed so the v2 documents do not overstate it. |

### Design-draft history (changes made between v1 drafts, before any code)

The v1 documents in `docs/` are the *final* v1 drafts. Earlier drafts differed as below; these rows are kept for completeness but are **not** deviations of the code from `docs/*_v1.md`, which already reflect the right-hand column.

| Area | Earlier v1 draft | Final v1 (= as built) | Reason |
|---|---|---|---|
| Database engine | PostgreSQL → SQL Server considered (Docker unavailable) | PostgreSQL | Brief's stated preference; native install replaced Docker |
| Claim query | `UPDLOCK, READPAST` (SQL Server draft) | CTE + `UPDATE … RETURNING` + `FOR UPDATE SKIP LOCKED` | Engine reverted |
| Evaluation scope | One hard-coded signature + test per language | Problem catalog (4 problems, 17 cases) + JSON harness; `GET /problems` | A single assertion demonstrates nothing about evaluation structure |
| API style | Minimal APIs + RFC 9457 `ProblemDetails` | Controllers + `Asp.Versioning` + `ApiResult<T>` + `CodeJudgeErrorCode` + `ExceptionMiddleware` | House-style consistency; one uniform contract |
| Enums | Lower-case strings mapped in the API layer | Enums end to end (`HasConversion<string>()`, `JsonStringEnumConverter(CamelCase)`) | No string literals |
| `evaluation_results` | `output` text | `output jsonb` + `tests_passed`, `tests_total`, `duration_ms` | Structured harness output; queryable counts |
| Integration test DB | SQLite in-memory | EF InMemory + no-op claimer | Deterministic; worker inert |

---

*CodeJudge — Project Status — September 2026 — Author: Vlachos Evangelos*
