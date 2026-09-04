# CodeJudge — Project Status

Tracks the milestones between the v1 design documents in [`docs/`](docs/) and a finished
implementation. The design docs are the source of truth; every place the code departs from them is
recorded in [Deviations from v1 design](#deviations-from-v1-design) so the v2 documents can be
brought back in line.

**Legend:** `[x]` done and building · `[~]` partial (see note) · `[ ]` not started

---

## Assignment brief — requirement by requirement

| Requirement | Status | Where |
|---|---|---|
| POST submission (`userId`, `problemId`, `code`, `language` ∈ C#/Python/JS) → id + `pending` | ✅ | `SubmissionsController.Create` |
| GET status/result by id — status, rubric items, output/errors | ✅ | `SubmissionsController.GetById` |
| Async evaluation: Compiles/Parses · Passes Basic Test · Security Check · per-item pass/fail + messages | ✅ | `EvaluationService`, evaluators, `RestrictedKeywordPolicy` |
| Background processing; API stays responsive | ✅ | `EvaluationWorker` + `SubmissionClaimer` (DB-as-queue, `SKIP LOCKED`) |
| Database (PostgreSQL preferred) with submissions, status, results, timestamps | ✅ | EF Core + Npgsql, migration `InitialCreate` |
| Project organisation: data / API / jobs / evaluation separated | ✅ | 4 projects, `DependencyRuleTests` |
| Async patterns throughout | ✅ | `async`/`await`, `CancellationToken` end to end |
| REST conventions, clear errors, Swagger/OpenAPI | ✅ | `ApiResult<T>`, `CodeJudgeErrorCode`, `ExceptionMiddleware`, Swashbuckle |
| API key on all endpoints | ✅ | `ApiKeyAuthenticationHandler` (`X-Api-Key`) |
| ≥ 2 unit tests on evaluation logic (compile failure, correct solution) | ✅ | `EvaluationServiceTests` (+ keyword, partial, timeout, crash), `CSharpEvaluatorTests` |
| *Stretch:* multiple languages | ✅ | C# (Roslyn), Python, JavaScript — all verified end to end |
| *Stretch:* paginated list per user | ✅ | `UsersController` |
| *Stretch:* logging | ✅ | Serilog; `SubmissionId`/`WorkerId` scopes in the worker |
| *Stretch:* Dockerfile | ✅ files · ⚠️ build not run | `Dockerfile`, `docker-compose.yml` |
| *Deliverable:* README with build/run/env/tests/DB | ✅ | `README.md` |
| *Deliverable:* REST API docs | ✅ | Swagger + `docs/API_Documentation.md` |
| *Deliverable:* schema / migration scripts | ✅ | `src/CodeJudge.Infrastructure/Persistence/Migrations` |
| *Deliverable:* sample curl / Postman | ✅ | `scripts/curl-samples.sh`, `scripts/verify-local.*`, `postman/CodeJudge.postman_collection.json` |
| *Deliverable:* architectural note ≤ 150 words | ✅ | `docs/ARCHITECTURAL_NOTE.md` (150 words) |

---

## M1 — Design documents

- [x] `docs/System_Design_v1.md`
- [x] `docs/Database_Design_v1.md`

## M2 — Solution skeleton

- [x] `CodeJudge.slnx`, `global.json` (SDK 10.0.x)
- [x] `Directory.Build.props` — `net10.0`, C# 14, nullable, implicit usings, `TreatWarningsAsErrors`
- [x] `Directory.Packages.props` — central package management
- [x] Four source projects + two test projects with the Clean Architecture reference graph
      (`Api → Application, Infrastructure`; `Infrastructure → Application → Domain`)

## M3 — Domain

- [x] `Enums/Language.cs`, `Enums/SubmissionStatus.cs`, `Enums/RubricItem.cs`
- [x] `Entities/Submission.cs` — aggregate root, state machine (`Create`/`Claim`/`Complete`/`Fail`)
- [x] `Entities/EvaluationResult.cs` — `Pass`/`Fail`/`Skip` factories
- [x] `Exceptions/DomainException.cs`, `InvalidStatusTransitionException.cs`, `UnsupportedLanguageException.cs`

## M4 — Application and persistence

- [x] `Abstractions/` — `ICodeEvaluator`, `IProblemCatalog`, `IRestrictedKeywordPolicy`,
      `ISubmissionClaimer`, `ISubmissionRepository`, `IUnitOfWork`
- [x] `Problems/ProblemCatalog.cs` — four problems, per-language signatures, JSON test cases
- [x] `Submissions/SubmissionService.cs`, DTOs, `SubmissionMapper`
- [x] `Submissions/Validators/CreateSubmissionCommandValidator.cs`
- [x] `Evaluation/EvaluationService.cs` — rubric order, short-circuit, skip semantics
- [x] `Evaluation/JsonValueComparer.cs`
- [x] `Common/CodeJudgeErrorCode.cs`, `PagedResult<T>`, `NotFoundException`
- [x] `Persistence/CodeJudgeDbContext.cs` + entity configurations (snake_case, `jsonb`, indexes)
- [x] `Persistence/SubmissionClaimer.cs` — `FOR UPDATE SKIP LOCKED` claim query
- [x] `Persistence/Migrations/InitialCreate`
- [x] `DependencyInjection.cs` in Application and Infrastructure

## M5 — API

- [x] `Controllers/V1/SubmissionsController.cs` — `POST /submissions`, `GET /submissions/{id}`
- [x] `Controllers/V1/ProblemsController.cs` — `GET /problems`
- [x] `Controllers/V1/UsersController.cs` — `GET /users/{userId}/submissions`
- [x] `Middleware/ExceptionMiddleware.cs` — exception → (status, `CodeJudgeErrorCode`)
- [x] `Filters/ValidationActionFilter.cs`
- [x] `Authentication/ApiKeyAuthenticationHandler.cs`, `ApiKeyOptions.cs`, `ApiKeySettings.cs`
- [x] `Swagger/ConfigureSwaggerOptions.cs`, `Swagger/ApiKeySecurityOperationFilter.cs`
- [x] `Models/Common/ApiResult.cs`, `ApiJson.cs`, `NumericEnumConverter.cs`
- [x] `Models/Requests/CreateSubmissionRequest.cs`
- [x] `Program.cs` — Serilog, versioning, auth, Swagger UI, health check, migrations on startup
- [x] `appsettings.json`, `appsettings.Development.json`

## M6 — Worker and evaluators

- [x] `Evaluators/ProcessRunner.cs` — hard timeout, process-tree kill
- [x] `Evaluators/Harness/harness.py`, `Evaluators/Harness/harness.js` (embedded resources)
- [x] `Evaluators/HarnessProtocol.cs` — stdout → `HarnessRunResult`
- [x] `Evaluators/PythonEvaluator.cs`
- [x] `Evaluators/JavaScriptEvaluator.cs` — `node --check` parse step, `harness.js` run (was a stub until the Step 3 verification)
- [x] `Security/RestrictedKeywordPolicy.cs`
- [x] `Workers/EvaluationSettings.cs`, `Workers/EvaluationWorker.cs`
- [x] `Evaluators/CSharpEvaluator.cs` — Roslyn compile with diagnostics, collectible `AssemblyLoadContext`, reflection invoke, `Task.WaitAsync` timeout

## M7 — Tests

- [x] `UnitTests/Evaluation/EvaluationServiceTests.cs` — fake `ICodeEvaluator`: compile failure, correct solution,
      restricted keyword, partial pass, timeout, harness crash, system errors
- [x] `UnitTests/Evaluation/CSharpEvaluatorTests.cs` — real Roslyn evaluator: diagnostics, wrapping, wrong answer, exception, timeout
- [x] `UnitTests/Domain/SubmissionStateMachineTests.cs`
- [x] `UnitTests/Problems/ProblemCatalogTests.cs`
- [x] `UnitTests/Validators/CreateSubmissionCommandValidatorTests.cs`
- [x] `UnitTests/Architecture/DependencyRuleTests.cs` — NetArchTest layer rules
- [x] `IntegrationTests/SubmissionFlowTests.cs` — `WebApplicationFactory` + EF InMemory

## M8 — Repository files

- [x] `docker-compose.yml` — PostgreSQL 17 (named volume, healthcheck) + API
- [x] `Dockerfile` — multi-stage sdk:10.0 → aspnet:10.0, python3 + nodejs, non-root user, port 8080
- [x] `postman/CodeJudge.postman_collection.json` — every endpoint, `baseUrl`/`apiKey` variables, POST saves `submissionId`
- [x] `scripts/curl-samples.sh`
- [x] `scripts/verify-local.sh`, `scripts/verify-local.ps1` — one-command end-to-end check (a–h), parameterised by base URL and API key
- [x] `docs/API_Documentation.md` — every endpoint with auth, request, response-code table and real captured examples
- [x] `docs/ARCHITECTURAL_NOTE.md` — 150 words
- [x] Final `README.md`
- [ ] `docker build` / `docker compose up` executed on a machine with a running Docker daemon

---

## Verification status

| Check | State |
|---|---|
| `dotnet build` | Succeeds, 0 warnings (`TreatWarningsAsErrors`) |
| `dotnet test` | All unit and integration tests pass; no database or external runtime required |
| Python submission end-to-end (`sum-two-numbers` → `completed`, `testsPassed = 4`) | Verified with `scripts/verify-local.sh` / `.ps1` against local PostgreSQL 18 (see deviation 10) |
| C# submission end-to-end | Verified end-to-end by the scripts; also covered by `CSharpEvaluatorTests` (real Roslyn, no runtime needed) |
| JavaScript submission end-to-end | Verified end-to-end by the scripts (`node` v24) |
| Error shapes 400/3001, 401/4001, 404/2001, 400/3002; `/problems`; user history | Verified by the scripts; 3001/3002/4001/2001 also covered by integration tests |
| `docker build .` | Dockerfile and compose file validated (`docker compose config`); image build not run — Docker Desktop daemon was not running on the dev machine |
| Swagger | Confirmed from `/swagger/v1/swagger.json`: one `v1` document, summary + response codes on all four actions, `ApiKey` security scheme applied to every operation, enums rendered as camelCase strings, example request body (`UseAllOfToExtendReferenceSchemas` keeps the `language` example) |

---

## Deviations from v1 design

Every row is a decision that differs from `docs/System_Design_v1.md` or `docs/Database_Design_v1.md`
and must be folded into the v2 documents.

| # | Area | v1 design says | Implementation does | Why |
|---|---|---|---|---|
| 1 | `ICodeEvaluator` signature (SD §6.5) | `Task<IReadOnlyList<TestCaseResult>> RunAsync(...)` | `Task<HarnessRunResult> RunAsync(...)` | A bare case list cannot express "the whole run timed out" or "the harness itself crashed with no per-case results". `HarnessRunResult` wraps the cases with `TimedOut`, `Stderr` and `DurationMs`, which `EvaluationService` needs to build the `Test` result. |
| 2 | Evaluation timeout config (SD §10) | one key `Evaluation:TimeoutSeconds` | two keys `Evaluation:CompileTimeoutSeconds` (10) and `Evaluation:RunTimeoutSeconds` (5) | Compiling and running have very different cost profiles — a cold Roslyn/`node --check` start is slower than the run it guards. One shared value would either be too tight for compile or too loose for untrusted execution. |
| 3 | Validator name (SD §6.7) | `CreateSubmissionRequestValidator` | `CreateSubmissionCommandValidator` | Validation runs on the Application-layer `CreateSubmissionCommand`, not the API request model, so the rules stay testable without referencing the API project. The action filter resolves the validator for the mapped command. |
| 4 | `EvaluationResult` factory names (DB §9) | `Passed` / `Failed` / `SkippedResult` | `Pass` / `Fail` / `Skip` | `Passed` collides with the `bool Passed` data property that the contract requires. The data property keeps the contract name; the factory verbs were shortened. |
| 5 | Startup configuration (SD §10) | config list has no migration key | added `Database:ApplyMigrationsOnStartup` (default `false`, `true` in Development) | Lets the Development environment and Docker Compose create the schema on boot without making that the behaviour in any other environment. Guarded so the EF InMemory provider used by tests is skipped. |
| 6 | C# execution harness (SD §6.5) | "user code + generated harness class compiled together; harness invoked via reflection" | user code alone is compiled; the harness loop (argument binding, invocation, JSON shaping) runs host-side by reflection over the loaded assembly | Keeps submitted code from being compiled against `System.Text.Json` and keeps the harness protocol in one place instead of duplicated in generated C#. The observable contract — per-case `actual`/`error`/`durationMs` — is identical to the Python and JavaScript harnesses. |
| 7 | C# submission shape (SD §6.5) | signature only, e.g. `int Sum(int a, int b)` | a bare method is accepted and wrapped in a generated `public class Solution`; code that already declares a type or namespace is compiled unchanged; `System`, `System.Collections.Generic`, `System.Linq`, `System.Text` are implicit global usings | The signature table implies a method, but a compilation unit cannot contain a bare method. Accepting both shapes keeps the documented signature honest without forcing boilerplate on the submitter. |
| 8 | C# timeout enforcement (SD §6.5) | "on timeout the context is unloaded and the case marked `Timed out`" | the run is reported as timed out and stops, but the runaway thread is abandoned; `AssemblyLoadContext.Unload()` is called and completes only once that thread ends | .NET has no safe thread abort. Already named in SD §11 as the in-process weak spot; recorded here because the unload is not guaranteed, only requested. |
| 9 | C# compile references (SD §6.5, unspecified) | — | submissions compile against a fixed allow-list: `System.Private.CoreLib`, `System.Runtime`, `System.Runtime.Extensions`, `System.Collections`, `System.Linq`, `System.Console`, `System.Text.RegularExpressions` | Referencing the whole trusted-platform set would expose `System.Diagnostics.Process`, `System.IO`, `System.Net`, … at compile time regardless of the keyword check. The list is the minimum the catalog problems need. |
| 10 | Database version / local setup (SD §6.11, §10) | PostgreSQL 17 on `localhost:5432`, `postgres`/`postgres` | verified locally against **PostgreSQL 18.6 on port 5433** with a dedicated `codejudge`/`codejudge` login owning database `codejudge`; `appsettings.Development.json` carries that connection string, `appsettings.json` and `docker-compose.yml` keep the design default (`postgres:17`, 5432) | The dev machine already had PostgreSQL 16 on 5432 and a fresh PostgreSQL 18 on 5433. A least-privilege login was created instead of using the superuser; the superuser password is not stored anywhere in the repo. Nothing in the schema or queries is version-specific (`FOR UPDATE SKIP LOCKED`, partial indexes, `jsonb` all exist since 9.5). |
| 11 | Binding errors → error code (SD §6.7, §6.10) | validation via FluentValidation; `3002 UnsupportedLanguage` raised by "Validator / `UnsupportedLanguageException`" | `ValidationActionFilter` also converts `ModelState` binding failures (malformed JSON, unknown enum value such as `"language": "cobol"`) into `ValidationException`; a failure on `language` carries `UnsupportedLanguage` → `400/3002`, any other → `400/1000` | With `SuppressModelStateInvalidFilter = true` the built-in 400 is gone, so binding failures otherwise reached the action with a null body and became a `500/0`. Routing them through the same exception path keeps "every 4xx is an `ApiResult`" true. |
