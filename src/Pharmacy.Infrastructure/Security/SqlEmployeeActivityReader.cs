using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Application.Security.Errors;
using Pharmacy.Application.Security.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Security;

public sealed class SqlEmployeeActivityReader : IEmployeeActivityReader
{
    private static readonly HashSet<string> StrongBusinessAuditActions = new(StringComparer.Ordinal)
    {
        "sale.finalize",
        "sale.return_refund"
    };

    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly ICurrentSecuritySession currentSession;

    public SqlEmployeeActivityReader(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IPermissionAuthorizer authorizer,
        ICurrentSecuritySession currentSession)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
    }

    public async Task<Result<IReadOnlyList<EmployeeActivitySummaryView>>> GetSummariesAsync(
        EmployeeActivityPeriod period,
        CancellationToken cancellationToken = default)
    {
        var authorization = authorizer.Authorize(SecurityPermissions.SecurityViewEmployeeActivity);
        if (authorization.IsFailure)
        {
            return Result<IReadOnlyList<EmployeeActivitySummaryView>>.Failure(authorization.Error);
        }

        if (!period.IsValid)
        {
            return Result<IReadOnlyList<EmployeeActivitySummaryView>>.Failure(SecurityErrors.InvalidActivityPeriod);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var values = await LoadSummariesCoreAsync(context, period, null, cancellationToken);
            return Result<IReadOnlyList<EmployeeActivitySummaryView>>.Success(Array.AsReadOnly(values));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<IReadOnlyList<EmployeeActivitySummaryView>>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    public async Task<Result<EmployeeActivityDetailView>> GetEmployeeAsync(
        long userId,
        EmployeeActivityPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Result<EmployeeActivityDetailView>.Failure(SecurityErrors.UserNotFound);
        }

        var actor = currentSession.User;
        var authorization = actor?.UserId == userId
            ? authorizer.Authorize(SecurityPermissions.SalesViewHistory)
            : authorizer.Authorize(SecurityPermissions.SecurityViewEmployeeActivity);
        if (authorization.IsFailure)
        {
            return Result<EmployeeActivityDetailView>.Failure(authorization.Error);
        }

        if (!period.IsValid)
        {
            return Result<EmployeeActivityDetailView>.Failure(SecurityErrors.InvalidActivityPeriod);
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var summaries = await LoadSummariesCoreAsync(context, period, userId, cancellationToken);
            if (summaries.Length == 0)
            {
                return Result<EmployeeActivityDetailView>.Failure(SecurityErrors.UserNotFound);
            }

            var sessions = await context.UserSessions.AsNoTracking()
                .Where(item => item.UserAccountId == userId)
                .OrderByDescending(item => item.StartedAtUtc)
                .ThenByDescending(item => item.Id)
                .Select(item => new EmployeeSessionHistoryView(
                    item.Id,
                    item.StartedAtUtc,
                    item.EndedAtUtc,
                    item.Status,
                    item.EndReason))
                .ToArrayAsync(cancellationToken);

            var sales = await context.Sales.AsNoTracking()
                .Where(sale => sale.FinalizedByUserAccountId == userId
                    && sale.CompletedAtUtc >= period.FromUtc
                    && sale.CompletedAtUtc < period.ToUtc)
                .OrderByDescending(sale => sale.CompletedAtUtc)
                .ThenByDescending(sale => sale.Id)
                .Select(sale => new EmployeeSaleHistoryView(
                    sale.Id,
                    sale.Reference,
                    sale.CompletedAtUtc,
                    sale.GrandTotal))
                .ToArrayAsync(cancellationToken);

            var activity = await LoadActivityAsync(context, userId, period, cancellationToken);
            return Result<EmployeeActivityDetailView>.Success(new EmployeeActivityDetailView(
                summaries[0],
                Array.AsReadOnly(sessions),
                Array.AsReadOnly(sales),
                Array.AsReadOnly(activity)));
        }
        catch (Exception exception) when (SecurityPersistenceSupport.IsDatabaseUnavailable(exception))
        {
            return Result<EmployeeActivityDetailView>.Failure(SecurityErrors.DatabaseUnavailable);
        }
    }

    private static async Task<EmployeeActivitySummaryView[]> LoadSummariesCoreAsync(
        PharmacyDbContext context,
        EmployeeActivityPeriod period,
        long? onlyUserId,
        CancellationToken cancellationToken)
    {
        var users = await context.UserAccounts.AsNoTracking()
            .Where(user => onlyUserId == null || user.Id == onlyUserId)
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Select(user => new
            {
                user.Id,
                user.Login,
                user.DisplayName,
                user.IsActive,
                user.LastLoginAtUtc,
                PrimaryRole = user.UserRoles
                    .Where(item => item.Role.IsActive)
                    .OrderBy(item => item.RoleId)
                    .Select(item => item.Role.Code)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        var sales = await context.Sales.AsNoTracking()
            .Where(sale => sale.FinalizedByUserAccountId != null
                && (onlyUserId == null || sale.FinalizedByUserAccountId == onlyUserId)
                && sale.CompletedAtUtc >= period.FromUtc
                && sale.CompletedAtUtc < period.ToUtc)
            .GroupBy(sale => sale.FinalizedByUserAccountId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Amount = group.Sum(item => item.GrandTotal) })
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var returns = await context.SaleReturns.AsNoTracking()
            .Where(item => item.PerformedByUserAccountId != null
                && (onlyUserId == null || item.PerformedByUserAccountId == onlyUserId)
                && item.CompletedAtUtc >= period.FromUtc
                && item.CompletedAtUtc < period.ToUtc)
            .GroupBy(item => item.PerformedByUserAccountId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count(), Amount = group.Sum(item => item.RefundAmount) })
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var openSessions = await context.UserSessions.AsNoTracking()
            .Where(item => item.Status == "Open" && (onlyUserId == null || item.UserAccountId == onlyUserId))
            .GroupBy(item => item.UserAccountId)
            .Select(group => new { UserId = group.Key, StartedAtUtc = group.Max(item => item.StartedAtUtc) })
            .ToDictionaryAsync(item => item.UserId, item => item.StartedAtUtc, cancellationToken);

        return users.Select(user =>
        {
            sales.TryGetValue(user.Id, out var sale);
            returns.TryGetValue(user.Id, out var returned);
            DateTime? currentSessionStartedAtUtc = openSessions.TryGetValue(user.Id, out var startedAtUtc)
                ? startedAtUtc
                : null;
            return new EmployeeActivitySummaryView(
                user.Id,
                user.Login,
                user.DisplayName,
                user.PrimaryRole ?? string.Empty,
                user.IsActive,
                user.LastLoginAtUtc,
                currentSessionStartedAtUtc,
                sale?.Count ?? 0,
                sale?.Amount ?? 0m,
                returned?.Count ?? 0,
                returned?.Amount ?? 0m);
        }).ToArray();
    }

    private static async Task<EmployeeActivityItemView[]> LoadActivityAsync(
        PharmacyDbContext context,
        long userId,
        EmployeeActivityPeriod period,
        CancellationToken cancellationToken)
    {
        var items = new List<EmployeeActivityItemView>();

        var authentication = await context.AuthenticationEvents.AsNoTracking()
            .Where(item => item.UserAccountId == userId
                && item.WasSuccessful
                && (item.EventType == "Login" || item.EventType == "Logout")
                && item.OccurredAtUtc >= period.FromUtc
                && item.OccurredAtUtc < period.ToUtc)
            .Select(item => new { item.Id, item.EventType, item.OccurredAtUtc })
            .ToArrayAsync(cancellationToken);
        items.AddRange(authentication.Select(item => new EmployeeActivityItemView(
            $"auth:{item.Id}",
            item.OccurredAtUtc,
            "Connexion",
            item.EventType == "Login" ? "Connexion réussie" : "Déconnexion",
            null,
            null,
            "Réussi")));

        var sales = await context.Sales.AsNoTracking()
            .Where(item => item.FinalizedByUserAccountId == userId
                && item.CompletedAtUtc >= period.FromUtc
                && item.CompletedAtUtc < period.ToUtc)
            .Select(item => new { item.Id, item.Reference, item.CompletedAtUtc, item.GrandTotal })
            .ToArrayAsync(cancellationToken);
        items.AddRange(sales.Select(item => new EmployeeActivityItemView(
            $"sale:{item.Id}", item.CompletedAtUtc, "Vente", "Vente finalisée",
            item.Reference, item.GrandTotal, "Réussi")));

        var returns = await context.SaleReturns.AsNoTracking()
            .Where(item => item.PerformedByUserAccountId == userId
                && item.CompletedAtUtc >= period.FromUtc
                && item.CompletedAtUtc < period.ToUtc)
            .Select(item => new { item.Id, item.Reference, item.CompletedAtUtc, item.RefundAmount })
            .ToArrayAsync(cancellationToken);
        items.AddRange(returns.Select(item => new EmployeeActivityItemView(
            $"return:{item.Id}", item.CompletedAtUtc, "Retour", "Retour remboursé",
            item.Reference, item.RefundAmount, "Réussi")));

        var audits = await context.AuditEvents.AsNoTracking()
            .Where(item => item.ActorUserId == userId
                && item.Result == "Success"
                && item.OccurredAtUtc >= period.FromUtc
                && item.OccurredAtUtc < period.ToUtc
                && !StrongBusinessAuditActions.Contains(item.Action))
            .Select(item => new
            {
                item.Id,
                item.OccurredAtUtc,
                item.Module,
                item.Action,
                item.EntityId
            })
            .ToArrayAsync(cancellationToken);
        items.AddRange(audits.Select(item => new EmployeeActivityItemView(
            $"audit:{item.Id}",
            item.OccurredAtUtc,
            Category(item.Module, item.Action),
            ActionLabel(item.Action),
            item.EntityId,
            null,
            "Réussi")));

        return items
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.BusinessReference, StringComparer.Ordinal)
            .ThenBy(item => item.ActivityId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Category(string module, string action) =>
        action.StartsWith("credit.", StringComparison.Ordinal) ? "Crédit" : module switch
        {
            "sales" => "Vente",
            "inventory" => "Stock",
            "purchasing" => "Achats",
            "customers" => "Crédit",
            "security" or "configuration" => "Administration",
            _ => "Activité"
        };

    private static string ActionLabel(string action) => action switch
    {
        "credit.payment" => "Règlement crédit enregistré",
        "sale.suspend" => "Vente suspendue",
        "sale.resume" => "Vente reprise",
        "sale.abandon" => "Vente suspendue abandonnée",
        "inventory.finalize" => "Inventaire finalisé",
        "inventory.adjust" => "Stock ajusté",
        "receipt.finalize" => "Réception finalisée",
        "order.create" => "Commande d’achat créée",
        "product.create" => "Produit créé",
        "product.update" => "Produit modifié",
        _ => "Opération enregistrée"
    };
}
