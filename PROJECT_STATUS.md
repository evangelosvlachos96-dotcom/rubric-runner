# CodeJudge — Project Status

Tracks the milestones between the v1 design documents in [`docs/`](docs/) and a finished
implementation. The design docs are the source of truth; every place the code departs from them is
recorded in [Deviations from v1 design](#deviations-from-v1-design) so the v2 documents can be
brought back in line.

**Legend:** `[x]` done and building · `[~]` partial (see note) · `[ ]` not started

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
- [x] `Evaluators/JavaScriptEvaluator.cs`
- [x] `Security/RestrictedKeywordPolicy.cs`
- [x] `Workers/EvaluationSettings.cs`, `Workers/EvaluationWorker.cs`
- [~] `Evaluators/CSharpEvaluator.cs` — registered stub: compile always succeeds, run reports "not implemented"; Roslyn + collectible `AssemblyLoadContext` still to do

## M7 — Tests

- [ ] `UnitTests/Evaluation/EvaluationServiceTests.cs` — compile failure, correct solution, and the
      partial / restricted-keyword / timeout / harness-crash paths
- [x] `UnitTests/Domain/SubmissionStateMachineTests.cs`
- [x] `UnitTests/Problems/ProblemCatalogTests.cs`
- [x] `UnitTests/Validators/CreateSubmissionCommandValidatorTests.cs`
- [ ] `UnitTests/Architecture/ArchitectureTests.cs` — NetArchTest layer rules
- [x] `IntegrationTests/SubmissionFlowTests.cs` — `WebApplicationFactory` + EF InMemory

## M8 — Repository files

- [ ] `docker-compose.yml` — PostgreSQL 17 + API
- [ ] `Dockerfile` — multi-stage build, Python and Node in the runtime image
- [ ] `postman/CodeJudge.postman_collection.json`
- [ ] `scripts/curl-samples.sh`
- [ ] `docs/API_Documentation.md`
- [ ] Final `README.md` — deliberately left as a placeholder until the implementation is signed off

---

## Verification status

| Check | State |
|---|---|
| `dotnet build` | Succeeds, 0 warnings (`TreatWarningsAsErrors`) |
| `dotnet test` | All unit and integration tests pass; no database or external runtime required |
| Python submission end-to-end (`sum-two-numbers` → `completed`, `testsPassed = 4`) | Requires a local PostgreSQL and `python` on `PATH`; not exercised in CI |
| C# submission end-to-end | Not yet — evaluator is a stub |
| JavaScript submission end-to-end | Requires `node` on `PATH`; not exercised in CI |

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
