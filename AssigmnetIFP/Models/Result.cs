namespace StudyResourceFinder.Models;

public abstract record Result<T, E>
{
    private Result() { }

    public sealed record Success : Result<T, E>
    {
        public T Value { get; }

        public Success(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Value = value;
        }

        public void Deconstruct(out T value) => value = Value;
    }

    public sealed record Failure : Result<T, E>
    {
        public E Error { get; }

        public Failure(E error)
        {
            ArgumentNullException.ThrowIfNull(error);
            Error = error;
        }

        public void Deconstruct(out E error) => error = Error;
    }

    public bool IsSuccess => this is Success;

    public static Result<T, E> Ok(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Success(value);
    }

    public static Result<T, E> Fail(E error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Failure(error);
    }

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
