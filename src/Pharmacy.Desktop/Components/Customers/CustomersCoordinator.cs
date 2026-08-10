using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Models;
using Pharmacy.Application.Customers.Queries;

namespace Pharmacy.Desktop.Components.Customers;

public sealed class CustomersCoordinator
{
    private readonly CustomerQueryHandlers queries;
    private readonly CustomerCommandHandlers commands;

    public CustomersCoordinator(CustomerQueryHandlers queries, CustomerCommandHandlers commands)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public IReadOnlyList<CustomerSummaryView> Customers { get; private set; } = Array.Empty<CustomerSummaryView>();

    public CustomerDetailView? SelectedCustomer { get; private set; }

    public string? UserMessage { get; private set; }

    public string? LastErrorCode { get; private set; }

    public bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await SearchAsync(null, cancellationToken);
    }

    public async Task SearchAsync(string? term, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ClearFeedback();

        try
        {
            var result = await queries.SearchAsync(new CustomerSearchQuery(term), cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code, result.Error.Message);
                return;
            }

            Customers = result.Value;
            UserMessage = Customers.Count == 0
                ? "Aucun client ne correspond à la recherche."
                : $"{Customers.Count} client(s) chargé(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectAsync(long customerId, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ClearFeedback();

        try
        {
            var result = await queries.GetByIdAsync(customerId, cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code, result.Error.Message);
                return;
            }

            SelectedCustomer = result.Value;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CreateAsync(
        string fullName,
        string? phone,
        bool creditAllowed,
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ClearFeedback();

        try
        {
            var result = await commands.CreateAsync(
                new CreateCustomerRequest(Guid.NewGuid(), fullName, phone, null, creditAllowed),
                cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code, result.Error.Message);
                return;
            }

            SelectedCustomer = result.Value;
            await SearchAsync(null, cancellationToken);
            UserMessage = "Client créé. L’autorisation crédit reste explicite et modifiable.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RegisterPaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        IsBusy = true;
        ClearFeedback();

        try
        {
            var result = await commands.RegisterCreditPaymentAsync(
                new RegisterCreditPaymentRequest(
                    Guid.NewGuid(),
                    SelectedCustomer.CustomerId,
                    amount,
                    Application.Sales.Models.SalePaymentMethod.Cash,
                    false),
                cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error.Code, result.Error.Message);
                return;
            }

            SelectedCustomer = result.Value;
            await SearchAsync(null, cancellationToken);
            UserMessage = "Règlement crédit enregistré dans le journal immuable.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearFeedback()
    {
        LastErrorCode = null;
        UserMessage = null;
    }

    private void ApplyFailure(string code, string message)
    {
        LastErrorCode = code;
        UserMessage = message;
    }
}
