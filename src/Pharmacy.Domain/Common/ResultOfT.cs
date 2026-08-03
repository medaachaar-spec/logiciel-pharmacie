namespace Pharmacy.Domain.Common;

public sealed class Result<T>
{
    private readonly T? value;

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        this.value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("Un résultat en échec ne contient pas de valeur.");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(true, value, Error.None);
    }

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.IsNone)
        {
            throw new ArgumentException("Un échec doit contenir une erreur active.", nameof(error));
        }

        return new Result<T>(false, default, error);
    }
}
