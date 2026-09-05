# CodeJudge — API Documentation

| | |
|---|---|
| **Version** | v1.0 |
| **Date** | September 2026 |
| **Project** | rubric-runner / CodeJudge |
| **Author** | Vlachos Evangelos |

All examples below are real responses captured from a running instance (ids and timestamps shortened).

---

## 1. Authentication

Every endpoint under `/api/v1/` requires a static API key in the `X-Api-Key` header. `GET /health` and the Swagger UI are anonymous.

| Property | Value |
|---|---|
| Header | `X-Api-Key: {key}` |
| Key source | `ApiKeySettings:Key` in `appsettings.json` (or environment variable `ApiKeySettings__Key`) — default `dev-api-key-change-me` |
| Scheme | ASP.NET Core `AuthenticationHandler<ApiKeyOptions>` named `ApiKey`; constant-time comparison |
| Failure | `401` with `WWW-Authenticate: ApiKey` and an `ApiResult` carrying error code `4001` |

```bash
curl -H "X-Api-Key: dev-api-key-change-me" http://localhost:5000/api/v1/problems
```

---

## 2. Base URL and Versioning

Endpoints are versioned by URL segment (`Asp.Versioning.Mvc`). Version `1.0` is the default and is written `v1` in URLs; Swagger groups documents per version.

| Context | URL |
|---|---|
| Base URL (local `dotnet run`) | `http://localhost:5000/api/v1` |
| Base URL (Docker Compose) | `http://localhost:8080/api/v1` |
| Submissions | `/api/v1/submissions` |
| Users | `/api/v1/users/{userId}/submissions` |
| Problems | `/api/v1/problems` |
| Health | `/health` |
| Swagger UI / document | `/swagger` · `/swagger/v1/swagger.json` |

Responses carry `api-supported-versions: 1.0`.

---

## 3. ApiResult Response Structure

Every API response — success and error, including `401` from the authentication handler — is an `ApiResult<T>` envelope. Clients check `status` first.

**Success**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": { "…": "…" },
  "error": null
}
```

**Error**

```json
{
  "status": false,
  "description": "Problem 'nope' does not exist.",
  "data": null,
  "error": {
    "errorCode": 3001,
    "description": "Problem 'nope' does not exist."
  }
}
```

| Field | Type | Meaning |
|---|---|---|
| `status` | boolean | `true` on success, `false` on error |
| `description` | string | Human-readable summary of the outcome |
| `data` | `T` \| null | Payload on success |
| `error.errorCode` | integer | Stable machine-readable code (see [§8](#8-error-codes-reference)) |
| `error.description` | string | Human-readable error text |

**Enums** are serialised as camelCase strings and accepted case-insensitively on input:

| Field | Values on the wire | Accepted on input |
|---|---|---|
| `language` | `cSharp`, `python`, `javaScript` | any casing, e.g. `csharp`, `CSharp`, `javascript` |
| `status` | `pending`, `evaluating`, `completed`, `error` | — |
| `rubricItem` | `security`, `compiles`, `test` | — |

---

## 4. Submissions Endpoints

### POST /api/v1/submissions

**Summary:** Submit a code solution. The submission is stored with status `pending` and evaluated asynchronously by the background worker.
**Auth:** Required

**Request body**

```json
{
  "userId": "demo",
  "problemId": "sum-two-numbers",
  "language": "python",
  "code": "def sum_two(a, b):\n    return a + b"
}
```

| Field | Rules |
|---|---|
| `userId` | Required, ≤ 100 chars, pattern `^[A-Za-z0-9_\-.@]+$` (client-asserted identity) |
| `problemId` | Required, must exist in the problem catalog (`GET /api/v1/problems`) |
| `language` | Required, one of `csharp`, `python`, `javascript` (case-insensitive) |
| `code` | Required, ≤ 65 536 characters. Python/JS: a top-level function with the catalog name. C#: a bare method or a class containing the method. |

**Response codes**

| Code | Description |
|---|---|
| 201 | Submission created and queued. `Location: /api/v1/submissions/{id}`. |
| 400 | Validation failed (`1000`), problem not found (`3001`), unsupported language (`3002`), code too large (`2002`). |
| 401 | Missing or invalid API key (`4001`). |

**Example response (201)**

```json
{
  "status": true,
  "description": "Submission accepted and queued for evaluation.",
  "data": {
    "id": "01a06c53-9b86-74d6-85fe-cf0ed3496c6c",
    "userId": "demo",
    "problemId": "sum-two-numbers",
    "language": "python",
    "status": "pending",
    "createdAt": "2026-09-04T12:10:13.766778Z",
    "startedAt": null,
    "completedAt": null,
    "errorMessage": null,
    "results": []
  },
  "error": null
}
```

**Example response (400 / 3001)**

```json
{
  "status": false,
  "description": "Problem 'nope' does not exist.",
  "data": null,
  "error": { "errorCode": 3001, "description": "Problem 'nope' does not exist." }
}
```

### GET /api/v1/submissions/{id}

**Summary:** Retrieve a submission's status and rubric results.
**Auth:** Required

| Parameter | In | Rules |
|---|---|---|
| `id` | path | GUID |

**Response codes**

| Code | Description |
|---|---|
| 200 | Submission found. `results` is empty while `pending` / `evaluating`; exactly three items (`security`, `compiles`, `test`) when `completed`; empty with `errorMessage` set when `error`. |
| 401 | Missing or invalid API key (`4001`). |
| 404 | Submission not found (`2001`). |

**Example response (200, completed, all passed)**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": {
    "id": "01a06c53-9b86-74d6-85fe-cf0ed3496c6c",
    "userId": "demo",
    "problemId": "sum-two-numbers",
    "language": "python",
    "status": "completed",
    "createdAt": "2026-09-04T12:10:13.766778Z",
    "startedAt": "2026-09-04T12:10:14.157219Z",
    "completedAt": "2026-09-04T12:10:18.350032Z",
    "errorMessage": null,
    "results": [
      { "rubricItem": "security", "passed": true, "skipped": false, "message": null, "output": null,
        "testsPassed": null, "testsTotal": null, "durationMs": null, "evaluatedAt": "2026-09-04T12:10:18.336660Z" },
      { "rubricItem": "compiles", "passed": true, "skipped": false, "message": "Compiled successfully.", "output": null,
        "testsPassed": null, "testsTotal": null, "durationMs": 503, "evaluatedAt": "2026-09-04T12:10:18.336661Z" },
      { "rubricItem": "test", "passed": true, "skipped": false, "message": "4/4 test cases passed",
        "output": {
          "cases": [
            { "id": 1, "args": [3, 4], "expected": 7, "actual": 7, "passed": true, "error": null, "durationMs": 0.0205 },
            { "id": 2, "args": [-2, 2], "expected": 0, "actual": 0, "passed": true, "error": null, "durationMs": 0.4008 },
            { "id": 3, "args": [0, 0], "expected": 0, "actual": 0, "passed": true, "error": null, "durationMs": 0.006 },
            { "id": 4, "args": [1000000, 1000000], "expected": 2000000, "actual": 2000000, "passed": true, "error": null, "durationMs": 0.0045 }
          ]
        },
        "testsPassed": 4, "testsTotal": 4, "durationMs": 1411, "evaluatedAt": "2026-09-04T12:10:18.349794Z" }
    ]
  },
  "error": null
}
```

**Example response (200, completed, compile failure)** — `status` is still `completed`: the rubric ran. `test` is `skipped`.

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": {
    "id": "01a06c53-987d-7215-8b0b-7ed774f196b7",
    "userId": "demo",
    "problemId": "sum-two-numbers",
    "language": "cSharp",
    "status": "completed",
    "createdAt": "2026-09-04T12:10:12.990180Z",
    "startedAt": "2026-09-04T12:10:14.157219Z",
    "completedAt": "2026-09-04T12:10:16.218883Z",
    "errorMessage": null,
    "results": [
      { "rubricItem": "security", "passed": true, "skipped": false, "message": null, "output": null,
        "testsPassed": null, "testsTotal": null, "durationMs": null, "evaluatedAt": "2026-09-04T12:10:16.203616Z" },
      { "rubricItem": "compiles", "passed": false, "skipped": false, "message": "; expected",
        "output": { "diagnostics": [ { "code": "CS1002", "line": 1, "column": 52, "message": "; expected" } ] },
        "testsPassed": null, "testsTotal": null, "durationMs": 1881, "evaluatedAt": "2026-09-04T12:10:16.211273Z" },
      { "rubricItem": "test", "passed": false, "skipped": true, "message": "Skipped because an earlier rubric item failed.",
        "output": null, "testsPassed": null, "testsTotal": null, "durationMs": null, "evaluatedAt": "2026-09-04T12:10:16.211362Z" }
    ]
  },
  "error": null
}
```

**Rubric result fields**

| Field | Meaning |
|---|---|
| `rubricItem` | `security` · `compiles` · `test` — always all three, in that order |
| `passed` | Outcome; `false` for skipped items too |
| `skipped` | `true` when an earlier item failed and this one did not run |
| `message` | Short summary: matched keyword and line, first compiler diagnostic, `k/N test cases passed`, `Timed out after N ms` |
| `output` | Structured detail: `{ "diagnostics": [...] }` for `compiles`, `{ "cases": [...] }` for `test`; `null` otherwise |
| `testsPassed` / `testsTotal` | `test` only |
| `durationMs` | Wall-clock time of that rubric step |

---

## 5. Users Endpoints

### GET /api/v1/users/{userId}/submissions

**Summary:** Paginated list of a user's submissions, newest first. List items are summaries — no rubric results.
**Auth:** Required

| Parameter | In | Rules | Default |
|---|---|---|---|
| `userId` | path | The `userId` used at submission time | — |
| `page` | query | ≥ 1 | `1` |
| `pageSize` | query | 1 – 100 | `20` |

**Response codes**

| Code | Description |
|---|---|
| 200 | Page returned (may be empty — an unknown user is simply an empty page). |
| 400 | Invalid paging parameters (`1000`). |
| 401 | Missing or invalid API key (`4001`). |

**Example response (200)**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": {
    "items": [
      { "id": "01a06c53-9b86-74d6-85fe-cf0ed3496c6c", "problemId": "sum-two-numbers", "language": "python", "status": "completed", "createdAt": "2026-09-04T12:10:13.766778Z" },
      { "id": "01a06c53-987d-7215-8b0b-7ed774f196b7", "problemId": "sum-two-numbers", "language": "cSharp", "status": "completed", "createdAt": "2026-09-04T12:10:12.990180Z" }
    ],
    "page": 1,
    "pageSize": 2,
    "totalCount": 17,
    "totalPages": 9
  },
  "error": null
}
```

**Example response (400 / 1000)** — `?page=0`

```json
{
  "status": false,
  "description": "Page must be 1 or greater.",
  "data": null,
  "error": { "errorCode": 1000, "description": "Page must be 1 or greater." }
}
```

---

## 6. Problems Endpoints

### GET /api/v1/problems

**Summary:** The problem catalog — everything a client can submit against, with the per-language function signature and the public sample case(s). Hidden test cases are not exposed here; they appear in `results[].output.cases` once a submission is evaluated.
**Auth:** Required

**Response codes**

| Code | Description |
|---|---|
| 200 | Catalog returned (4 problems). |
| 401 | Missing or invalid API key (`4001`). |

**Example response (200, first item shown)**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": [
    {
      "id": "sum-two-numbers",
      "title": "Sum Two Numbers",
      "description": "Return the sum of two integers a and b.",
      "difficulty": "Easy",
      "signatures": [
        { "language": "cSharp",     "functionName": "Sum",     "signature": "int Sum(int a, int b)" },
        { "language": "python",     "functionName": "sum_two", "signature": "def sum_two(a, b)" },
        { "language": "javaScript", "functionName": "sumTwo",  "signature": "function sumTwo(a, b)" }
      ],
      "sampleCases": [
        { "id": 1, "args": [3, 4], "expected": 7 }
      ]
    }
  ],
  "error": null
}
```

---

## 7. Health

### GET /health

**Summary:** Liveness/readiness — checks PostgreSQL connectivity (`AspNetCore.HealthChecks.NpgSql`).
**Auth:** Not required.

| Code | Body |
|---|---|
| 200 | `Healthy` (`text/plain`) |
| 503 | `Unhealthy` (`text/plain`) |

This is the standard ASP.NET Core health-check response, **not** an `ApiResult`.

---

## 8. Error Codes Reference

| Code | Name | HTTP | Raised when |
|---|---|---|---|
| 0 | Unknown | 500 | Unhandled exception. Safe message only; details logged with `TraceId`. |
| 1000 | ValidationFailed | 400 | FluentValidation failure, invalid paging, malformed request body, or a domain rule violation. All messages are returned, joined with ` \| `. |
| 1001 | NotFound | 404 | Generic not found. |
| 1002 | Conflict | 409 | Illegal submission state transition. |
| 2001 | SubmissionNotFound | 404 | No submission with that id. |
| 2002 | CodeTooLarge | 400 | `code` exceeds 65 536 characters. |
| 3001 | ProblemNotFound | 400 | `problemId` is not in the catalog. |
| 3002 | UnsupportedLanguage | 400 | `language` is not `csharp`, `python` or `javascript`. |
| 4001 | Unauthorized | 401 | Missing or invalid `X-Api-Key`. |

`4xx` responses are logged at *Warning*, `5xx` at *Error* with the request `TraceId`; exception details never reach the client.

---

## 9. Swagger Authentication Guide

| Step | Action |
|---|---|
| 1 | Run the API (`dotnet run --project src/CodeJudge.Api`) and open <http://localhost:5000/swagger> (Docker: port `8080`). |
| 2 | Click the **Authorize** button (padlock) at the top right of the page. |
| 3 | In the **ApiKey** field paste the value of `ApiKeySettings:Key` — `dev-api-key-change-me` by default — click **Authorize**, then **Close**. |
| 4 | Expand `POST /api/v1/submissions`, click **Try it out**; the body is pre-filled with a Python `sum-two-numbers` example. Click **Execute** and copy `data.id` from the `201` response. |
| 5 | Expand `GET /api/v1/submissions/{id}`, paste the id, **Execute**. Repeat after a couple of seconds until `data.status` is `completed` and inspect `results`. |
| 6 | `GET /api/v1/problems` lists every problem and signature you can submit against; `GET /api/v1/users/{userId}/submissions` shows your history. |
| 7 | To test the `401`, click **Authorize** → **Logout** and execute any request again. |

---

*CodeJudge — API Documentation v1.0 — September 2026*
