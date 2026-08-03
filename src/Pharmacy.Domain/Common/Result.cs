namespace Pharmacy.Domain.Common;

public sealed class Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success()
    {
        return new Result(true, Error.None);
    }

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.IsNone)
        {
            throw new ArgumentException("Un échec doit contenir une erreur active.", nameof(error));
        }

        return new Result(false, error);
    }
}
