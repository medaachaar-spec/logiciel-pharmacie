using System.Security.Cryptography;
using Pharmacy.Application.Security.Contracts;

namespace Pharmacy.Infrastructure.Security;

internal sealed class CryptographicRecoveryCodeGenerator : IRecoveryCodeGenerator
{
    public IReadOnlyList<string> Generate(int count)
    {
        if (count is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(count));
        var codes = new string[count];
        for (var index = 0; index < count; index++)
        {
            var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            codes[index] = string.Join('-', Enumerable.Range(0, 8).Select(part => value.Substring(part * 4, 4)));
        }
        return Array.AsReadOnly(codes);
    }
}
