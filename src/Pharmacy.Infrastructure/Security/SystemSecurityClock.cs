using Pharmacy.Application.Security.Contracts;

namespace Pharmacy.Infrastructure.Security;

internal sealed class SystemSecurityClock : ISecurityClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
