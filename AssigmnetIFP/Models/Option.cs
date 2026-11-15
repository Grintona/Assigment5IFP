using System;

namespace StudyResourceFinder.Models;

public abstract record Option<T>
{
    private Option() { }

    public sealed record Some(T Value) : Option<T>;

    public sealed record None : Option<T>;

    public bool IsSome => this is Some;
    public bool IsNone => this is None;

    public static Option<T> Return(T? value) =>
        value is null ? new None() : new Some(value);

    public static Option<T> SomeValue(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Some(value);
    }

    public static Option<T> NoneValue() => new None();

    public Option<U> Map<U>(Func<T, U> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return this switch
        {
            Some s => Option<U>.SomeValue(transform(s.Value)),
            None => Option<U>.NoneValue(),
            _ => throw new InvalidOperationException("Unknown Option state.")
        };
    }

    public Option<U> Bind<U>(Func<T, Option<U>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return this switch
        {
            Some s => next(s.Value),
            None => Option<U>.NoneValue(),
            _ => throw new InvalidOperationException("Unknown Option state.")
        };
    }

    public R Match<R>(Func<T, R> onSome, Func<R> onNone)
    {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);
        return this switch
        {
            Some s => onSome(s.Value),
            None => onNone(),
            _ => throw new InvalidOperationException("Unknown Option state.")
        };
    }

    public Result<T, E> ToResult<E>(E errorIfNone)
    {
        return this switch
        {
            Some s => Result<T, E>.Ok(s.Value),
            None => Result<T, E>.Fail(errorIfNone),
            _ => throw new InvalidOperationException("Unknown Option state.")
        };
    }
}
