namespace StudyResourceFinder.Models;

public sealed record DomainError(string Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}