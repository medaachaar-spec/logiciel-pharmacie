using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Models;
using Pharmacy.Application.Sales.Errors;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Infrastructure.Sales.InMemory;

public sealed class InMemorySaleDraftStore : ISaleDraftStore
{
    private readonly Dictionary<SaleDraftId, DraftSnapshot> drafts = new();
    private readonly object syncRoot = new();

    public Task<Result<VersionedSaleDraft>> CreateAsync(
        SaleDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = Snapshot(draft, 1);

        lock (syncRoot)
        {
            if (drafts.ContainsKey(draft.Id))
            {
                return Task.FromResult(
                    Result<VersionedSaleDraft>.Failure(
                        SalesApplicationErrors.DraftAlreadyExists(draft.Id)));
            }

            drafts.Add(draft.Id, snapshot);
            return Task.FromResult(
                Result<VersionedSaleDraft>.Success(Rehydrate(snapshot)));
        }
    }

    public Task<Result<VersionedSaleDraft>> GetAsync(
        SaleDraftId draftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            return Task.FromResult(drafts.TryGetValue(draftId, out var snapshot)
                ? Result<VersionedSaleDraft>.Success(Rehydrate(snapshot))
                : Result<VersionedSaleDraft>.Failure(
                    SalesApplicationErrors.DraftNotFound(draftId)));
        }
    }

    public Task<Result<VersionedSaleDraft>> SaveAsync(
        SaleDraft draft,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();

        if (expectedVersion <= 0)
        {
            return Task.FromResult(
                Result<VersionedSaleDraft>.Failure(
                    SalesApplicationErrors.InvalidExpectedVersion));
        }

        lock (syncRoot)
        {
            if (!drafts.TryGetValue(draft.Id, out var current))
            {
                return Task.FromResult(
                    Result<VersionedSaleDraft>.Failure(
                        SalesApplicationErrors.DraftNotFound(draft.Id)));
            }

            if (current.Version != expectedVersion)
            {
                return Task.FromResult(
                    Result<VersionedSaleDraft>.Failure(
                        SalesApplicationErrors.ConcurrencyConflict(
                            expectedVersion,
                            current.Version)));
            }

            var updated = Snapshot(draft, checked(current.Version + 1));
            drafts[draft.Id] = updated;
            return Task.FromResult(
                Result<VersionedSaleDraft>.Success(Rehydrate(updated)));
        }
    }

    private static DraftSnapshot Snapshot(SaleDraft draft, long version)
    {
        return new DraftSnapshot(
            draft.Id,
            version,
            draft.Lines
                .Select(line => new DraftLineSnapshot(
                    line.ProductSnapshot,
                    line.Quantity))
                .ToArray());
    }

    private static VersionedSaleDraft Rehydrate(DraftSnapshot snapshot)
    {
        var draft = SaleDraft.Create(snapshot.Id);

        foreach (var line in snapshot.Lines)
        {
            var mutation = draft.AddItem(line.Product, line.Quantity);

            if (mutation.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Le snapshot interne du brouillon est invalide : {mutation.Error.Code}.");
            }
        }

        return new VersionedSaleDraft(draft, snapshot.Version);
    }

    private sealed record DraftSnapshot(
        SaleDraftId Id,
        long Version,
        DraftLineSnapshot[] Lines);

    private sealed record DraftLineSnapshot(
        SaleProductSnapshot Product,
        SaleQuantity Quantity);
}
