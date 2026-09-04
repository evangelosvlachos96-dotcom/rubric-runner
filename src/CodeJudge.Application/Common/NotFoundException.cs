namespace CodeJudge.Application.Common;

/// <summary>
/// Thrown by application services when a requested entity does not exist.
/// <see cref="EntityName"/> lets the API middleware pick a specific error code
/// (e.g. <c>SubmissionNotFound</c> vs the generic <c>NotFound</c>).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}
