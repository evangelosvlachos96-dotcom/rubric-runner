namespace CodeJudge.Application.Abstractions;

/// <summary>
/// Commits pending changes. Implemented directly by the EF Core <c>DbContext</c>, which
/// already is the unit of work (System Design §6.3).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
