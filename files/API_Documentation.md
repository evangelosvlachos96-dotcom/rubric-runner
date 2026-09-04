# CodeJudge — API Documentation

| | |
|---|---|
| **Version** | v1.0 |
| **Date** | September 2026 |
| **Project** | RubricRunner / CodeJudge |
| **Author** | Vlachos Evangelos |

---

## 1. Authentication

All endpoints under `/api/v1/` require a static API key in the `X-Api-Key` header. `GET /health` and the Swagger UI are anonymous.

| Property | Value |
|---|---|
| Header | `X-Api-Key: {key}` |
| Key source | `ApiKeySettings:Key` (appsettings / environment `ApiKeySettings__Key`) |
| Scheme | ASP.NET Core `AuthenticationHandler` named `ApiKey`; constant-time comparison |
| Failure | `401` with `ApiResult` error code `4001` |

---

## 2. Base URL and Versioning

Endpoints are versioned by URL segment using `Asp.Versioning`. Version `1.0` is the default.

| Context | URL |
|---|---|
| Base URL (local) | `https://localhost:{port}/api/v1` |
| Base URL (Docker) | `http://localhost:8080/api/v1` |
| Submissions | `/api/v1/submissions` |
| Users | `/api/v1/users` |
| Problems | `/api/v1/problems` |
| Health | `/health` |
| Swagger UI | `/swagger` |

---

## 3. ApiResult Response Structure

Every response uses a consistent `ApiResult<T>` wrapper. Clients should check `status` before reading `data`.

**Success**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": { ... },
  "error": null
}
```

**Error**

```json
{
  "status": false,
  "description": null,
  "data": null,
  "error": {
    "errorCode": 3001,
    "description": "Problem 'nope' was not found."
  }
}
```

Enums are serialised as camelCase strings and accepted case-insensitively on input:

| Field | Values |
|---|---|
| `language` | `csharp`, `python`, `javascript` |
| `status` | `pending`, `evaluating`, `completed`, `error` |
| `rubricItem` | `security`, `compiles`, `test` |

---

## 4. Submissions Endpoints

### POST /api/v1/submissions

**Summary:** Submit a code solution. The submission is persisted with status `pending` and evaluated asynchronously by the background worker.
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
| `userId` | Required, ≤ 100 chars, pattern `^[A-Za-z0-9_\-.@]+$` |
| `problemId` | Required, must exist in the problem catalog |
| `language` | Required, one of `csharp`, `python`, `javascript` |
| `code` | Required, ≤ 65 536 characters |

| Code | Description |
|---|---|
| 201 | Submission created. `Location` header points to `GET /api/v1/submissions/{id}`. |
| 400 | Validation failed (`1000`), problem not found (`3001`), unsupported language (`3002`), code too large (`2002`). |
| 401 | Missing or invalid API key (`4001`). |

**Example response (201)**

```json
{
  "status": true,
  "description": "Submission accepted and queued for evaluation.",
  "data": {
    "id": "019213f0-7b1e-7c4a-9d3e-6f8a1b2c3d4e",
    "userId": "demo",
    "problemId": "sum-two-numbers",
    "language": "python",
    "status": "pending",
    "createdAt": "2026-09-04T12:00:00.000Z",
    "startedAt": null,
    "completedAt": null,
    "errorMessage": null,
    "results": []
  },
  "error": null
}
```

### GET /api/v1/submissions/{id:guid}

**Summary:** Retrieve a submission's status and rubric results.
**Auth:** Required

| Code | Description |
|---|---|
| 200 | Submission found. `results` is empty while `pending`/`evaluating`, exactly three items when `completed`. |
| 401 | Missing or invalid API key (`4001`). |
| 404 | Submission not found (`2001`). |

**Example response (200, completed)**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": {
    "id": "019213f0-7b1e-7c4a-9d3e-6f8a1b2c3d4e",
    "userId": "demo",
    "problemId": "sum-two-numbers",
    "language": "python",
    "status": "completed",
    "createdAt": "2026-09-04T12:00:00.000Z",
    "startedAt": "2026-09-04T12:00:01.900Z",
    "completedAt": "2026-09-04T12:00:02.350Z",
    "errorMessage": null,
    "results": [
      { "rubricItem": "security", "passed": true,  "skipped": false, "message": null, "output": null, "testsPassed": null, "testsTotal": null, "durationMs": 1 },
      { "rubricItem": "compiles", "passed": true,  "skipped": false, "message": null, "output": null, "testsPassed": null, "testsTotal": null, "durationMs": 48 },
      { "rubricItem": "test",     "passed": true,  "skipped": false, "message": "4/4 test cases passed",
        "output": { "cases": [ { "id": 1, "args": [3, 4], "expected": 7, "actual": 7, "passed": true, "error": null, "durationMs": 0.02 } ] },
        "testsPassed": 4, "testsTotal": 4, "durationMs": 120 }
    ]
  },
  "error": null
}
```

**Example response (200, compile failure)** — note `status` is still `completed`; the rubric ran.

```json
{
  "status": "completed",
  "results": [
    { "rubricItem": "security", "passed": true,  "skipped": false },
    { "rubricItem": "compiles", "passed": false, "skipped": false, "message": "CS1002: ; expected (line 1)", "output": { "diagnostics": [ { "line": 1, "column": 42, "code": "CS1002", "message": "; expected" } ] } },
    { "rubricItem": "test",     "passed": false, "skipped": true }
  ]
}
```

---

## 5. Users Endpoints

### GET /api/v1/users/{userId}/submissions

**Summary:** Paginated list of a user's submissions, newest first. Results are not included in list items.
**Auth:** Required

| Query | Rules | Default |
|---|---|---|
| `page` | ≥ 1 | `1` |
| `pageSize` | 1–100 | `20` |

| Code | Description |
|---|---|
| 200 | Page returned (may be empty). |
| 400 | Invalid paging parameters (`1000`). |
| 401 | Missing or invalid API key (`4001`). |

**Example response (200)**

```json
{
  "status": true,
  "description": "🚀 Houston, we don't have a problem",
  "data": {
    "items": [
      { "id": "019213f0-…", "problemId": "sum-two-numbers", "language": "python", "status": "completed", "createdAt": "2026-09-04T12:00:00.000Z", "completedAt": "2026-09-04T12:00:02.350Z" }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  },
  "error": null
}
```

---

## 6. Problems Endpoints

### GET /api/v1/problems

**Summary:** The problem catalog — what can be submitted, with per-language signatures and the sample test case.
**Auth:** Required

| Code | Description |
|---|---|
| 200 | Catalog returned. |
| 401 | Missing or invalid API key (`4001`). |

**Example response (200, one item shown)**

```json
{
  "status": true,
  "data": [
    {
      "id": "two-sum",
      "title": "Two Sum",
      "description": "Return the indices of the two numbers that add up to target.",
      "difficulty": "Easy",
      "signatures": {
        "csharp": "int[] TwoSum(int[] nums, int target)",
        "python": "def two_sum(nums, target)",
        "javascript": "function twoSum(nums, target)"
      },
      "sampleCases": [ { "args": [[2, 7, 11, 15], 9], "expected": [0, 1] } ]
    }
  ],
  "error": null
}
```

---

## 7. Health

### GET /health

**Auth:** Not required. Returns `200 Healthy` when the API can reach PostgreSQL, `503 Unhealthy` otherwise. Standard ASP.NET Core health-check response, not an `ApiResult`.

---

## 8. Error Codes Reference

| Code | Name | HTTP | Description |
|---|---|---|---|
| 0 | Unknown | 500 | Unexpected server error. Safe message only; details logged with `TraceId`. |
| 1000 | ValidationFailed | 400 | FluentValidation failure or domain rule violation. All messages returned, joined with ` \| `. |
| 1001 | NotFound | 404 | Generic not found. |
| 1002 | Conflict | 409 | Invalid state transition. |
| 2001 | SubmissionNotFound | 404 | No submission with that id. |
| 2002 | CodeTooLarge | 400 | `code` exceeds 65 536 characters. |
| 3001 | ProblemNotFound | 400 | `problemId` is not in the catalog. |
| 3002 | UnsupportedLanguage | 400 | `language` is not `csharp`, `python` or `javascript`. |
| 4001 | Unauthorized | 401 | Missing or invalid `X-Api-Key`. |

---

## 9. Swagger Authentication Guide

| Step | Action |
|---|---|
| 1 | Run the API and open `https://localhost:{port}/swagger`. |
| 2 | Click the green **Authorize** button (padlock) at the top of the page. |
| 3 | In the **ApiKey** field paste the value of `ApiKeySettings:Key` (default `dev-api-key-change-me`) and click **Authorize**, then **Close**. |
| 4 | Expand `POST /api/v1/submissions`, click **Try it out**, use the example body, **Execute**. Copy `data.id`. |
| 5 | Expand `GET /api/v1/submissions/{id}`, paste the id, **Execute**. Repeat after a couple of seconds until `status` is `completed`. |
| 6 | `GET /api/v1/problems` lists every problem and signature you can submit against. |

---

*CodeJudge — API Documentation v1.0 — September 2026*
