#!/usr/bin/env bash
# End-to-end verification of a running CodeJudge API (System Design §2 acceptance criteria).
#
#   ./scripts/verify-local.sh [BASE_URL] [API_KEY]
#   BASE_URL=http://localhost:5000 API_KEY=dev-api-key-change-me ./scripts/verify-local.sh
#
# Requires curl and python3 (for JSON parsing). Exits non-zero on the first failed check.
set -uo pipefail

BASE_URL="${1:-${BASE_URL:-http://localhost:5000}}"
API_KEY="${2:-${API_KEY:-dev-api-key-change-me}}"
POLL_SECONDS="${POLL_SECONDS:-2}"
POLL_LIMIT_SECONDS="${POLL_LIMIT_SECONDS:-30}"

PY=python3; command -v python3 >/dev/null 2>&1 || PY=python

PASS=0; FAIL=0
ok()   { PASS=$((PASS+1)); echo "  [PASS] $1"; }
fail() { FAIL=$((FAIL+1)); echo "  [FAIL] $1"; echo "         $2"; exit 1; }

# json <json> <python-expression over d>
json() { printf '%s' "$1" | $PY -c "import sys,json; d=json.load(sys.stdin); print($2)" 2>/dev/null; }

# request <method> <path> [body] [--no-key]   → sets STATUS, BODY, HEADERS
request() {
  local method="$1" path="$2" body="${3:-}" nokey="${4:-}"
  local args=(-s -o "$TMP_BODY" -D "$TMP_HDR" -w '%{http_code}' -X "$method" "$BASE_URL$path" -H "Content-Type: application/json")
  [ -z "$nokey" ] && args+=(-H "X-Api-Key: $API_KEY")
  [ -n "$body" ] && args+=(-d "$body")
  STATUS=$(curl "${args[@]}")
  BODY=$(cat "$TMP_BODY")
  HEADERS=$(cat "$TMP_HDR")
}

TMP_BODY=$(mktemp); TMP_HDR=$(mktemp)
trap 'rm -f "$TMP_BODY" "$TMP_HDR"' EXIT

submit() { # submit <language> <code-json-string> [problemId] → sets SUBMIT_ID, asserts 201 pending
  local lang="$1" code="$2" problem="${3:-sum-two-numbers}"
  request POST /api/v1/submissions "{\"userId\":\"demo\",\"problemId\":\"$problem\",\"language\":\"$lang\",\"code\":$code}"
  [ "$STATUS" = "201" ] || fail "POST $lang → 201" "got $STATUS: $BODY"
  [ "$(json "$BODY" 'd["status"]')" = "True" ] || fail "POST $lang ApiResult.status true" "$BODY"
  [ "$(json "$BODY" 'd["data"]["status"]')" = "pending" ] || fail "POST $lang data.status pending" "$BODY"
  echo "$HEADERS" | grep -qi '^Location: .*/api/v1/submissions/' || fail "POST $lang Location header" "$HEADERS"
  SUBMIT_ID=$(json "$BODY" 'd["data"]["id"]')
}

wait_completed() { # wait_completed <id> → BODY holds the final submission
  local id="$1" waited=0
  while :; do
    request GET "/api/v1/submissions/$id"
    local st; st=$(json "$BODY" 'd["data"]["status"]')
    if [ "$st" = "completed" ] || [ "$st" = "error" ]; then return 0; fi
    [ "$waited" -ge "$POLL_LIMIT_SECONDS" ] && fail "submission $id completes within ${POLL_LIMIT_SECONDS}s" "last status: $st"
    sleep "$POLL_SECONDS"; waited=$((waited+POLL_SECONDS))
  done
}

print_results() {
  json "$BODY" '"\n".join("         %-9s passed=%-5s skipped=%-5s tests=%s/%s  %s" % (r["rubricItem"], r["passed"], r["skipped"], r.get("testsPassed"), r.get("testsTotal"), (r.get("message") or "")[:60]) for r in d["data"]["results"])'
}

assert_all_passed() {
  [ "$(json "$BODY" 'd["data"]["status"]')" = "completed" ] || fail "$1 status completed" "$BODY"
  [ "$(json "$BODY" 'all(r["passed"] for r in d["data"]["results"])')" = "True" ] || fail "$1 all three rubric items passed" "$(print_results)"
  [ "$(json "$BODY" '[r for r in d["data"]["results"] if r["rubricItem"]=="test"][0]["testsPassed"]')" = "4" ] || fail "$1 testsPassed = 4" "$(print_results)"
}

echo "CodeJudge verification against $BASE_URL"

echo "a) GET /health"
request GET /health "" nokey
[ "$STATUS" = "200" ] && ok "health 200" || fail "health 200" "got $STATUS: $BODY"

echo "b) POST python submission"
submit python '"def sum_two(a, b):\n    return a + b"'; PY_ID=$SUBMIT_ID
ok "201 pending, Location header, id=$PY_ID"

echo "c) poll python submission"
wait_completed "$PY_ID"; assert_all_passed "python"; ok "python completed, 4/4"; print_results

echo "d) javascript submission"
submit javascript '"function sumTwo(a, b) { return a + b; }"'; JS_ID=$SUBMIT_ID
wait_completed "$JS_ID"; assert_all_passed "javascript"; ok "javascript completed, 4/4"; print_results

echo "e) csharp submission"
submit csharp '"public static class Solution { public static int Sum(int a, int b) => a + b; }"'; CS_ID=$SUBMIT_ID
wait_completed "$CS_ID"; assert_all_passed "csharp"; ok "csharp completed, 4/4"; print_results

echo "f) restricted keyword"
submit python '"import os\nos.system('"'"'ls'"'"')\ndef sum_two(a, b):\n    return a + b"'; SEC_ID=$SUBMIT_ID
wait_completed "$SEC_ID"
[ "$(json "$BODY" 'd["data"]["status"]')" = "completed" ] || fail "security case completed" "$BODY"
[ "$(json "$BODY" '[r["passed"] for r in d["data"]["results"] if r["rubricItem"]=="security"][0]')" = "False" ] || fail "security.passed=false" "$(print_results)"
[ "$(json "$BODY" 'all(r["skipped"] for r in d["data"]["results"] if r["rubricItem"]!="security")')" = "True" ] || fail "compiles/test skipped" "$(print_results)"
ok "security failed, other two skipped"; print_results

echo "g) error shapes"
request POST /api/v1/submissions '{"userId":"demo","problemId":"nope","language":"python","code":"x = 1"}'
[ "$STATUS" = "400" ] && [ "$(json "$BODY" 'd["error"]["errorCode"]')" = "3001" ] && ok "unknown problem → 400/3001" || fail "unknown problem → 400/3001" "got $STATUS: $BODY"
request POST /api/v1/submissions '{"userId":"demo","problemId":"sum-two-numbers","language":"python","code":"x = 1"}' nokey
[ "$STATUS" = "401" ] && [ "$(json "$BODY" 'd["error"]["errorCode"]')" = "4001" ] && ok "no api key → 401/4001" || fail "no api key → 401/4001" "got $STATUS: $BODY"
request GET "/api/v1/submissions/$($PY -c 'import uuid;print(uuid.uuid4())')"
[ "$STATUS" = "404" ] && [ "$(json "$BODY" 'd["error"]["errorCode"]')" = "2001" ] && ok "random id → 404/2001" || fail "random id → 404/2001" "got $STATUS: $BODY"
request POST /api/v1/submissions '{"userId":"demo","problemId":"sum-two-numbers","language":"cobol","code":"x = 1"}'
CODE=$(json "$BODY" 'd["error"]["errorCode"]')
[ "$STATUS" = "400" ] && { [ "$CODE" = "3002" ] || [ "$CODE" = "1000" ]; } && ok "language cobol → 400/$CODE" || fail "language cobol → 400/3002|1000" "got $STATUS: $BODY"

echo "h) catalog and user history"
request GET /api/v1/problems
[ "$STATUS" = "200" ] && [ "$(json "$BODY" 'len(d["data"])')" = "4" ] && ok "problems → 200, 4 problems" || fail "problems → 200 with 4" "got $STATUS: $BODY"
request GET "/api/v1/users/demo/submissions?page=1&pageSize=10"
IDS=$(json "$BODY" '",".join(i["id"] for i in d["data"]["items"])')
for id in "$PY_ID" "$JS_ID" "$CS_ID" "$SEC_ID"; do
  case "$IDS" in *"$id"*) ;; *) fail "user history contains $id" "$BODY" ;; esac
done
[ "$STATUS" = "200" ] && ok "user history → 200 containing the 4 submissions" || fail "user history → 200" "got $STATUS"

echo
echo "All $PASS checks passed."
