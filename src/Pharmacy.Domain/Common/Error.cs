namespace Pharmacy.Domain.Common;

public sealed record Error
{
    public static Error None { get; } = new();

    private Error()
    {
        Code = string.Empty;
        Message = string.Empty;
    }

    public Error(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code.Trim();
        Message = message.Trim();
    }

    public string Code { get; }

    public string Message { get; }

    public bool IsNone => Code.Length == 0;
}
