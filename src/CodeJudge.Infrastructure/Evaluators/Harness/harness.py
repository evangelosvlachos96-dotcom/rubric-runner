# CodeJudge Python harness.
#
# This script is appended AFTER the user's submitted code, so the user's function is
# already defined in this module's global namespace. It implements the language-agnostic
# JSON protocol (System Design 6.5):
#
#   stdin  -> { "function": "sum_two", "cases": [ { "id": 1, "args": [3, 4] }, ... ] }
#   stdout <- { "results": [ { "id": 1, "actual": 7, "error": null, "durationMs": 0.04 }, ... ] }
#
# All test cases run in this single process; per-case errors are captured, never raised.

import sys
import json
import time


def _run():
    data = json.loads(sys.stdin.read())
    fn_name = data["function"]
    cases = data["cases"]
    fn = globals().get(fn_name)

    results = []
    for case in cases:
        cid = case["id"]
        args = case["args"]
        actual = None
        error = None
        start = time.perf_counter()
        try:
            if not callable(fn):
                raise NameError("function '%s' is not defined" % fn_name)
            actual = fn(*args)
            # Ensure the result is JSON-serialisable; otherwise report it as an error.
            json.dumps(actual)
        except Exception as exc:  # noqa: BLE001 - the harness must never crash on user code
            actual = None
            error = "%s: %s" % (type(exc).__name__, exc)
        duration_ms = round((time.perf_counter() - start) * 1000.0, 4)
        results.append({"id": cid, "actual": actual, "error": error, "durationMs": duration_ms})

    sys.stdout.write(json.dumps({"results": results}))


_run()
