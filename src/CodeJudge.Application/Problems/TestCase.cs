using System.Text.Json;

namespace CodeJudge.Application.Problems;

/// <summary>
/// One test case for a problem. Arguments and the expected value are stored as
/// language-neutral JSON so the same case runs unchanged against every language harness
/// (System Design §6.5). <see cref="IsSample"/> cases are the only ones exposed publicly
/// via <c>GET /problems</c>.
/// </summary>
public sealed record TestCase(int Id, JsonElement[] Args, JsonElement Expected, bool IsSample);
