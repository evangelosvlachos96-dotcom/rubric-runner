// CodeJudge JavaScript harness.
//
// This script is appended AFTER the user's submitted code, so the user's function is already
// declared in this module's scope. It implements the language-agnostic JSON protocol
// (System Design 6.5):
//
//   stdin  -> { "function": "sumTwo", "cases": [ { "id": 1, "args": [3, 4] }, ... ] }
//   stdout <- { "results": [ { "id": 1, "actual": 7, "error": null, "durationMs": 0.04 }, ... ] }
//
// All test cases run in this single process; per-case errors are captured, never thrown.

(function () {
  let input = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', function (chunk) { input += chunk; });
  process.stdin.on('end', function () {
    const data = JSON.parse(input);

    let fn = null;
    try {
      // The user's function is declared in this same scope; resolve it by name.
      // eslint-disable-next-line no-eval
      fn = eval(data.function);
    } catch (e) {
      fn = null;
    }

    const results = data.cases.map(function (testCase) {
      let actual = null;
      let error = null;
      const start = process.hrtime.bigint();
      try {
        if (typeof fn !== 'function') {
          throw new Error("function '" + data.function + "' is not defined");
        }
        const value = fn.apply(null, testCase.args);
        actual = value === undefined ? null : value;
      } catch (e) {
        actual = null;
        error = (e && e.message) ? e.message : String(e);
      }
      const durationMs = Number(process.hrtime.bigint() - start) / 1e6;
      return { id: testCase.id, actual: actual, error: error, durationMs: Math.round(durationMs * 10000) / 10000 };
    });

    process.stdout.write(JSON.stringify({ results: results }));
  });
})();
