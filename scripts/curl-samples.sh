#!/usr/bin/env bash
# One curl per CodeJudge endpoint. Override the defaults with BASE_URL / API_KEY.
#
#   BASE_URL=http://localhost:5000 API_KEY=dev-api-key-change-me ./scripts/curl-samples.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"
API_KEY="${API_KEY:-dev-api-key-change-me}"

echo "== GET /health (anonymous)"
curl -s -i "$BASE_URL/health"
echo; echo

echo "== GET /api/v1/problems"
curl -s "$BASE_URL/api/v1/problems" -H "X-Api-Key: $API_KEY"
echo; echo

echo "== POST /api/v1/submissions (Python)"
CREATE_RESPONSE=$(curl -s "$BASE_URL/api/v1/submissions" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"sum-two-numbers","language":"python","code":"def sum_two(a, b):\n    return a + b"}')
echo "$CREATE_RESPONSE"
echo

# Pull the id out of the ApiResult envelope without needing jq.
SUBMISSION_ID=$(printf '%s' "$CREATE_RESPONSE" | sed -n 's/.*"id":"\([0-9a-f-]\{36\}\)".*/\1/p')
echo "submission id: $SUBMISSION_ID"
echo

echo "== POST /api/v1/submissions (JavaScript)"
curl -s "$BASE_URL/api/v1/submissions" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"sum-two-numbers","language":"javaScript","code":"function sumTwo(a, b) { return a + b; }"}'
echo; echo

echo "== POST /api/v1/submissions (C#)"
curl -s "$BASE_URL/api/v1/submissions" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"sum-two-numbers","language":"cSharp","code":"public static class Solution { public static int Sum(int a, int b) => a + b; }"}'
echo; echo

echo "== GET /api/v1/submissions/{id}"
curl -s "$BASE_URL/api/v1/submissions/$SUBMISSION_ID" -H "X-Api-Key: $API_KEY"
echo; echo

echo "== GET /api/v1/users/{userId}/submissions?page=1&pageSize=10"
curl -s "$BASE_URL/api/v1/users/demo/submissions?page=1&pageSize=10" -H "X-Api-Key: $API_KEY"
echo; echo

echo "== Error shapes"
echo "-- 401 (no key)"
curl -s "$BASE_URL/api/v1/problems"
echo
echo "-- 400 / 3001 (unknown problem)"
curl -s "$BASE_URL/api/v1/submissions" \
  -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" \
  -d '{"userId":"demo","problemId":"nope","language":"python","code":"x = 1"}'
echo
echo "-- 404 / 2001 (unknown submission)"
curl -s "$BASE_URL/api/v1/submissions/00000000-0000-0000-0000-000000000000" -H "X-Api-Key: $API_KEY"
echo
