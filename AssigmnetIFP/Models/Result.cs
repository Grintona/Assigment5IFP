namespace StudyResourceFinder.Models;

public abstract record Result<T, E>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T, E>;

    public sealed record Failure(E Error) : Result<T, E>;

    public bool IsSuccess => this is Success;

    public static Result<T, E> Ok(T value) => new Success(value);

    public static Result<T, E> Fail(E error) => new Failure(error);

    public Result<U, E> Map<U>(Func<T, U> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return this switch
        {
            Success success => Result<U, E>.Ok(transform(success.Value)),
            Failure failure => Result<U, E>.Fail(failure.Error),
            _ => throw new InvalidOperationException("Unknown result case.")
        };
    }

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return this switch
        {
            Success success => next(success.Value),
            Failure failure => Result<U, E>.Fail(failure.Error),
            _ => throw new InvalidOperationException("Unknown result case.")
        };
    }
}