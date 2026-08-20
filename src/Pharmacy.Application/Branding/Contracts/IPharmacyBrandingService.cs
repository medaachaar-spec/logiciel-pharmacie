using Pharmacy.Application.Branding.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Branding.Contracts;

public interface IPharmacyBrandingService
{
    Task<Result<PharmacyBrandingView>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<PharmacyBrandingView>> UpdateAsync(
        UpdatePharmacyBrandingRequest request,
        CancellationToken cancellationToken = default);
}
