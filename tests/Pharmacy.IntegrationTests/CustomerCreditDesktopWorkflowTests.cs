using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Contracts;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Customers.Queries;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Desktop.Components.Customers;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class CustomerCreditDesktopWorkflowTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task CRD016_022_DoubleSubmitCreatesOneSettlementAndExternalCardStoresNoCardData()
    {
        var store = new BlockingCustomerStore();
        var coordinator = new CustomersCoordinator(
            new CustomerQueryHandlers(store, TestPermissionAuthorizer.AllowAll),
            new CustomerCommandHandlers(store, store, TestPermissionAuthorizer.AllowAll));
        await coordinator.SelectAsync(42, TestContext.Current.CancellationToken);

        var first = coordinator.RegisterPaymentAsync(
            20m,
            SalePaymentMethod.ExternalCard,
            true,
            TestContext.Current.CancellationToken);
        await store.PaymentStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var duplicate = coordinator.RegisterPaymentAsync(
            20m,
            SalePaymentMethod.ExternalCard,
            true,
            TestContext.Current.CancellationToken);

        await duplicate;
        Assert.True(coordinator.IsPaymentInProgress);
        store.ReleasePayment.TrySetResult();
        await first;

        Assert.Equal(1, store.PaymentCalls);
        Assert.NotNull(store.LastPayment);
        Assert.Equal(SalePaymentMethod.ExternalCard, store.LastPayment.PaymentMethod);
        Assert.True(store.LastPayment.ExternalCardConfirmed);
        Assert.Equal(80m, coordinator.SelectedCustomer!.CreditBalance.Amount);
        Assert.False(coordinator.IsPaymentInProgress);
        Assert.DoesNotContain(
            typeof(RegisterCreditPaymentRequest).GetProperties(),
            property => property.Name.Contains("Pan", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Cvv", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("CardNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CRD_UI_ExposesBalanceHistoryModesValidationAndMixedPaymentDue()
    {
        var customersPage = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "Pharmacy.Desktop",
            "Components",
            "Pages",
            "CustomersPage.razor"));
        var paymentPanel = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "Pharmacy.Desktop",
            "Components",
            "Pos",
            "DemoPaymentPanel.razor"));

        Assert.Contains("Solde crédit", customersPage, StringComparison.Ordinal);
        Assert.Contains("Historique", customersPage, StringComparison.Ordinal);
        Assert.Contains("Aucune dette à régler", customersPage, StringComparison.Ordinal);
        Assert.Contains("Carte — TPE externe", customersPage, StringComparison.Ordinal);
        Assert.Contains("amount <= Coordinator.SelectedCustomer.CreditBalance.Amount", customersPage, StringComparison.Ordinal);
        Assert.Contains("Coordinator.IsPaymentInProgress", customersPage, StringComparison.Ordinal);
        Assert.Contains("Session.ImmediatePaymentDue", paymentPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("Value < Session.Total", paymentPanel, StringComparison.Ordinal);
    }

    private static CustomerDetailView Detail(decimal balance) => new(
        42,
        "CLIENT-QA-CREDIT-01",
        "Client QA crédit",
        null,
        null,
        true,
        true,
        Money.Create(balance).Value,
        new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
        [1],
        Array.Empty<CustomerLedgerEntryView>());

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Pharmacy.slnx est introuvable.");
    }

    private sealed class BlockingCustomerStore : ICustomerStore, ICreditPaymentStore
    {
        public TaskCompletionSource PaymentStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleasePayment { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int PaymentCalls { get; private set; }
        public RegisterCreditPaymentRequest? LastPayment { get; private set; }

        public Task<Result<CustomerDetailView>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail(100m)));

        public Task<Result<CustomerDetailView>> UpdateAsync(UpdateCustomerRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail(100m)));

        public Task<Result<IReadOnlyList<CustomerSummaryView>>> SearchAsync(CustomerSearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<CustomerSummaryView>>.Success(Array.Empty<CustomerSummaryView>()));

        public Task<Result<CustomerDetailView>> GetByIdAsync(long customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CustomerDetailView>.Success(Detail(100m)));

        public async Task<Result<CustomerDetailView>> RegisterPaymentAsync(
            RegisterCreditPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            PaymentCalls++;
            LastPayment = request;
            PaymentStarted.TrySetResult();
            await ReleasePayment.Task.WaitAsync(cancellationToken);
            return Result<CustomerDetailView>.Success(Detail(80m));
        }
    }
}
