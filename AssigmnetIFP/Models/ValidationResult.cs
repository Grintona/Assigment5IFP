using System.Collections.Immutable;

namespace StudyResourceFinder.Models;

public abstract record ValidationResult<T>
{
    private ValidationResult() { }

    public sealed record Valid(T Value) : ValidationResult<T>;

    public sealed record Invalid(ImmutableArray<string> Reasons) : ValidationResult<T>;
}