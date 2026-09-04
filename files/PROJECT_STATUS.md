# CodeJudge — Project Status

| | |
|---|---|
| **Snapshot date** | 4 September 2026 |
| **Design** | `docs/System_Design_v1.md`, `docs/Database_Design_v1.md` (as designed) · `docs/System_Design_v2.md`, `docs/Database_Design_v2.md` (as built) |
| **Status** | Functional end to end. All core requirements implemented; verification against a live PostgreSQL and the Docker image build are the items not confirmed on the author's machine at submission time. |

This file is the honest state of the repository at submission. Each milestone maps to a section of the design documents.

---

## Assignment brief — requirement by requirement

| Requirement | Status | Where |
|---|---|---|
| POST submission (userId, problemId, code, language ∈ C#/Python/JS) → id + `pending` | ✅ | `SubmissionsController.Create` |
| GET status/result by id — status, rubric items, output/errors | ✅ | `SubmissionsController.GetById` |
| Async evaluation: Compiles/Parses · Passes Basic Test · Security Check · per-item pass/fail + messages | ✅ | `EvaluationService`, evaluators, `RestrictedKeywordPolicy` |
| Background processing; API stays responsive | ✅ | `EvaluationWorker` + `SubmissionClaimer` (DB-as-queue, `SKIP LOCKED`) |
| Database (PostgreSQL preferred) with submissions, status, results, timestamps | ✅ | EF Core + Npgsql, migration `InitialCreate` |
| Project organisation: data / API / jobs / evaluation separated | ✅ | 4 projects, NetArchTest-enforced |
| Async patterns throughout | ✅ | — |
| REST conventions, clear errors, Swagger/OpenAPI | ✅ | `ApiResult<T>`, `CodeJudgeErrorCode`, `ExceptionMiddleware`, Swashbuckle |
| API key on all endpoints | ✅ | `ApiKeyAuthenticationHandler` (`X-Api-Key`) |
| ≥ 2 unit tests on evaluation logic (compile failure, correct solution) | ✅ | `EvaluationServiceTests` (+ keyword, partial pass), `CSharpEvaluatorTests` |
| *Stretch:* multiple languages | ✅ | C# (Roslyn), Python, JavaScript |
| *Stretch:* paginated list per user | ✅ | `UsersController` |
| *Stretch:* logging (Serilog) | ✅ | `Program.cs`, worker scopes |
| *Stretch:* Dockerfile | ✅ files / ⚠️ build not verified | `Dockerfile`, `docker-compose.yml` (Docker Desktop unavailable on dev machine) |
| *Deliverable:* README with build/run/env/tests/DB | ✅ | `README.md` |
| *Deliverable:* REST API docs | ✅ | Swagger + `docs/API_Documentation.md` |
| *Deliverable:* schema / migration scripts | ✅ | `src/CodeJudge.Infrastructure/Persistence/Migrations` |
| *Deliverable:* sample curl / Postman | ✅ | `scripts/curl-samples.sh`, `scripts/verify-local.*`, `CodeJudge.postman_collection.json` |
| *Deliverable:* architectural note ≤ 150 words | ✅ | `docs/ARCHITECTURAL_NOTE.md` (149 words) |

---

## Milestones

### ✅ M1 — Design
- [x] Architecture options with pros/cons; Clean Architecture (lean) selected — *SD §5*
- [x] Use cases, sequence, state machine, failure scenarios — *SD §3, §4, §7, §9*
- [x] Decision tables for jobs, evaluation, security, validation, auth, API style, database — *SD §6*
- [x] Database design: ER, 3NF, data dictionary, CRUD matrix, claim query, indexes, evolution — *DD §2–§11*

### ✅ M2 — Skeleton & Domain
- [x] Solution, 4 + 2 projects, `Directory.Build.props`, central package management, `global.json`
- [x] Enums, `Submission` aggregate with `Create/Claim/Complete/Fail` and guards, `EvaluationResult`, domain exceptions

### ✅ M3 — Application
- [x] `CodeJudgeErrorCode`, `NotFoundException`, `PagedResult<T>`, abstractions
- [x] `ProblemCatalog` — 4 problems, 17 test cases
- [x] `EvaluationService` (ordered, short-circuit, always three results)
- [x] `SubmissionService`, DTOs, `CreateSubmissionCommandValidator`, `AddApplication()`

### ✅ M4 — Infrastructure: persistence
- [x] `CodeJudgeDbContext` (Npgsql, snake_case, `IUnitOfWork`), entity configurations, check constraints, partial + covering indexes
- [x] `SubmissionRepository`, `SubmissionClaimer` (CTE + `UPDATE … RETURNING … FOR UPDATE SKIP LOCKED`)
- [x] `InitialCreate` migration; guarded startup migration
- [x] `RestrictedKeywordPolicy` from configuration

### ✅ M5 — API
- [x] `ApiResult<T>`, `CreateSubmissionRequest`
- [x] `SubmissionsController`, `UsersController`, `ProblemsController` (v1, `Asp.Versioning`, XML docs, `ProducesResponseType`)
- [x] `ExceptionMiddleware`, `ValidationActionFilter` (+ `SuppressModelStateInvalidFilter`)
- [x] `ApiKeyAuthenticationHandler`, Swagger security definition, version-grouped docs
- [x] `Program.cs` wiring, Serilog, health check, `appsettings.*`

### ✅ M6 — Worker + evaluators + harness
- [x] `EvaluationSettings` (validated on start), `EvaluationWorker`
- [x] `ProcessRunner`, `harness.py`, `harness.js` (embedded)
- [x] `PythonEvaluator`, `JavaScriptEvaluator`, `CSharpEvaluator` (Roslyn + collectible `AssemblyLoadContext`)

### ✅ M7 — Tests
- [x] `SubmissionStateMachineTests`, `ProblemCatalogTests`, `CreateSubmissionCommandValidatorTests`
- [x] `EvaluationServiceTests` (brief-required: compile failure, correct solution; plus keyword, partial), `CSharpEvaluatorTests`
- [x] `DependencyRuleTests` (NetArchTest)
- [x] Integration: `CodeJudgeWebApplicationFactory` (EF InMemory, no-op claimer), `SubmissionFlowTests`

### 🔶 M8 — Repo files + final verification
- [x] `Dockerfile`, `docker-compose.yml` (validated with `docker compose config`)
- [x] `CodeJudge.postman_collection.json`, `scripts/curl-samples.sh`, `scripts/verify-local.ps1/.sh`
- [x] `README.md`, `docs/API_Documentation.md`, `docs/ARCHITECTURAL_NOTE.md`
- [x] `docs/System_Design_v2.md`, `docs/Database_Design_v2.md`
- [ ] **End-to-end run against live PostgreSQL** — blocked at submission time by local credentials for the pre-existing PostgreSQL 16 service; unit (53) and integration (3) tests are green. Run `scripts/verify-local.ps1` once the connection string in `appsettings.Development.json` is correct.
- [ ] **`docker build` / `docker compose up`** — not verified (Docker Desktop unavailable); files reviewed and compose config validated.

---

## Known limitations

- C# submissions execute **in-process** (collectible `AssemblyLoadContext` + timeout) — not a sandbox. Python/JS run in child processes killed on timeout — better, not isolation. Next step: containerised runner (SD v2 §7).
- The restricted-keyword scan is a **rubric item, not a security control**; trivially bypassed by string concatenation.
- `userId` is client-asserted under the static API key.
- Verified against PostgreSQL 16; design and compose target 17 (no 17-specific features used).

---

## Deviations from v1 design (source for the v2 "As Built" documents — now folded into `docs/*_v2.md`)

| Area | v1 (first draft) | As built | Reason |
|---|---|---|---|
| Database engine | PostgreSQL → SQL Server (Docker unavailable) → PostgreSQL | PostgreSQL 16 locally; design targets 17 | Returned to brief preference once native install available |
| Evaluation scope | One hard-coded signature + test per language | Problem catalog (4 problems, 17 cases) + JSON harness; `GET /problems` | Extensible evaluation, usable platform |
| API style | Minimal APIs + RFC 9457 `ProblemDetails` | Controllers + `Asp.Versioning` + `ApiResult<T>` + `CodeJudgeErrorCode` + `ExceptionMiddleware` | House-style consistency; one uniform contract |
| Enums | Lower-case strings in API layer | Enums end to end (`HasConversion<string>`, `JsonStringEnumConverter(CamelCase)`) | No string literals |
| `evaluation_results` | `output` text | `output jsonb` + `tests_passed`, `tests_total`, `duration_ms` | Structured harness output; queryable counts |
| Timeouts | Single `TimeoutSeconds` | `CompileTimeoutSeconds` 10 + `RunTimeoutSeconds` 5 | Different cost profiles |
| Lock duration | 60 s | 90 s | Cover compile + run with margin |
| Startup migrations | Always in Development | Config-guarded, skipped for non-relational provider | Integration tests share `Program` |
| Integration test DB | SQLite | EF InMemory + no-op claimer | Deterministic; same approach as VIP |
| Docker verification | Assumed | Compose validated, image build not run | Docker Desktop unavailable |

---

*CodeJudge — Project Status — September 2026 — Author: Vlachos Evangelos*
