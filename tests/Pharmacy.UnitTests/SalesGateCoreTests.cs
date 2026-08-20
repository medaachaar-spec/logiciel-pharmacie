using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SalesGateCoreTests
{
    [Fact]
    public void SAL0606_ExternalCardApplicationContractsContainNoSensitiveCardData()
    {
        var forbidden = new[] { "pan", "cvv", "cardnumber", "trackdata", "pin", "secret" };
        var contractTypes = new[]
        {
            typeof(FinalizeSaleRequest),
            typeof(SalePaymentRequest),
            typeof(SalePaymentView),
            typeof(FinalizedSaleView)
        };

        var names = contractTypes
            .SelectMany(type => type.GetProperties().Select(property => property.Name))
            .Select(name => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant())
            .ToArray();

        Assert.DoesNotContain(names, name => forbidden.Any(name.Contains));
    }

    [Fact]
    public async Task SAL0610_SalesHistoryRequiresSalesViewHistoryBeforeReaderAccess()
    {
        var reader = new RecordingSalesReader();
        var denied = new DenyAuthorizer();

        var byId = await new GetSaleByIdHandler(reader, denied)
            .HandleAsync(new GetSaleByIdQuery(1), TestContext.Current.CancellationToken);
        var recent = await new GetRecentSalesHandler(reader, denied)
            .HandleAsync(new GetRecentSalesQuery(10), TestContext.Current.CancellationToken);
        var returns = await new GetSaleReturnPreparationHandler(reader, denied)
            .HandleAsync(new GetSaleReturnPreparationQuery(1), TestContext.Current.CancellationToken);

        Assert.All(new[] { byId.Error.Code, recent.Error.Code, returns.Error.Code }, code =>
            Assert.Equal("security.authorization.denied", code));
        Assert.Equal(0, reader.CallCount);
        Assert.Equal(3, denied.Requests.Count);
        Assert.All(denied.Requests, permission => Assert.Equal(SecurityPermissions.SalesViewHistory, permission));
    }

    private sealed class DenyAuthorizer : IPermissionAuthorizer
    {
        public List<string> Requests { get; } = [];

        public Result Authorize(string permission, bool requireRecentReauthentication = false)
        {
            Requests.Add(permission);
            return Result.Failure(new Error("security.authorization.denied", "Permission refusée."));
        }
    }

    private sealed class RecordingSalesReader : ISalesReader, ISaleReturnReader
    {
        public int CallCount { get; private set; }

        public Task<Result<FinalizedSaleView>> GetByIdAsync(
            long saleId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Le garde aurait dû refuser avant le lecteur.");
        }

        public Task<Result<IReadOnlyList<FinalizedSaleSummaryView>>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Le garde aurait dû refuser avant le lecteur.");
        }

        public Task<Result<SaleReturnPreparationView>> GetPreparationAsync(
            long saleId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Le garde aurait dû refuser avant le lecteur.");
        }
    }
}
