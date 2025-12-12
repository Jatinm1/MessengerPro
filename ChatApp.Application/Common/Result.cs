namespace ChatApp.Application.Common;

/// <summary>
/// Generic result wrapper for operations that return a value.
/// Provides a consistent pattern for handling success/failure scenarios.
/// </summary>
/// <typeparam name="T">The type of value returned on success.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The value returned by a successful operation.
    /// Null when IsSuccess is false.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Error message describing why the operation failed.
    /// Null when IsSuccess is true.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result<T> Failure(string error) => new(false, default, error);
}

/// <summary>
/// Non-generic result wrapper for operations that don't return a value.
/// Used for commands or operations where only success/failure status matters.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Error message describing why the operation failed.
    /// Null when IsSuccess is true.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with no error message.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result Failure(string error) => new(false, error);
}