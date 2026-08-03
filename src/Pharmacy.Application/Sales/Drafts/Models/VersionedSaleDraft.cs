using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales.Drafts.Models;

public sealed record VersionedSaleDraft(SaleDraft Draft, long Version);
