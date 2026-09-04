# Evaluator unit tests — planned (Phase 2)

Per System Design §12, the evaluator tests are scheduled for Phase 2 (they exercise real
`python`/`node`/Roslyn execution). Planned cases:

- C# compile failure returns diagnostics; `Compiles = fail`, `Test = skipped`.
- A correct solution passes all catalog cases (`Test.testsPassed == testsTotal`).
- A partially-correct solution reports `k/n` cases passed.
- A restricted keyword is rejected before compile (`Security = fail`, others skipped).
- An infinite loop times out and is recorded as `Test = fail (Timed out …)`, worker unaffected.
- `JsonValueComparer` treats `[0,1]` (Python) and `[0, 1]` (JS) as equal, and `7` == `7.0`.
