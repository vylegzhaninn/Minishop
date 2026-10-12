namespace MiniShop.SharedKernel;

public sealed record Error(string Code, string Message)
{
    public static Error NotFound(string message) => new("not_found", message);
    public static Error Validation(string message) => new("validation", message);
    public static Error Conflict(string message) => new("conflict", message);
}

public readonly record struct Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) { Value = value; Error = null; }
    private Result(Error error) { Value = default; Error = error; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(Error error) => new(error);

    public TResult Match<TResult>(Func<T, TResult> onOk, Func<Error, TResult> onFail)
        => IsSuccess ? onOk(Value!) : onFail(Error!);
}
