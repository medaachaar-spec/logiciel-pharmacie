using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Sales.Drafts;

internal static class DraftVersionGuard
{
    public static Result Check(VersionedSaleDraft versionedDraft, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(versionedDraft);

        if (expectedVersion <= 0)
        {
            return Result.Failure(SalesApplicationErrors.InvalidExpectedVersion);
        }

        return versionedDraft.Version == expectedVersion
            ? Result.Success()
            : Result.Failure(
                SalesApplicationErrors.ConcurrencyConflict(
                    expectedVersion,
                    versionedDraft.Version));
    }
}
