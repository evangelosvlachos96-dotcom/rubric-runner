using CodeJudge.Application.Problems;

namespace CodeJudge.Application.Abstractions;

/// <summary>The in-code catalog of problems (System Design §6.5). A repository in a future evolution step.</summary>
public interface IProblemCatalog
{
    IReadOnlyList<ProblemDefinition> GetAll();

    ProblemDefinition? Find(string id);

    bool Exists(string id);
}
