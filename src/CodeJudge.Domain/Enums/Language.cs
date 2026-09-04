namespace CodeJudge.Domain.Enums;

/// <summary>
/// A programming language a submission can be written in.
/// Stored in the database as text via <c>HasConversion&lt;string&gt;()</c>
/// and serialised on the wire in camelCase (Database Design §6).
/// </summary>
public enum Language
{
    CSharp,
    Python,
    JavaScript,
}
