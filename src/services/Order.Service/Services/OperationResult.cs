namespace MicroservicesLab.OrderService.Services;

public enum OperationErrorType
{
    BadRequest,
    DependencyUnavailable
}

public sealed class OperationResult<T>
{
    private OperationResult(T? value, string? error, OperationErrorType errorType)
    {
        Value = value;
        Error = error;
        ErrorType = errorType;
    }

    public T? Value { get; }

    public string? Error { get; }

    public OperationErrorType ErrorType { get; }

    public bool IsSuccess => Error is null;

    public static OperationResult<T> Success(T value) => new(value, null, OperationErrorType.BadRequest);

    public static OperationResult<T> Failure(
        string error,
        OperationErrorType errorType = OperationErrorType.BadRequest) => new(default, error, errorType);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, OperationErrorType, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!, ErrorType);
    }
}
