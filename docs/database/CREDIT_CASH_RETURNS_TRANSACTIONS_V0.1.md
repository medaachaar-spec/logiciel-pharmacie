# Credit, Cash and Returns Transactions V0.1

## Sale With Credit

`SqlSaleFinalizer` keeps the existing FEFO sale transaction and adds:

- customer validation when credit is used;
- `CreditTotal` and `CustomerId` on the sale;
- a `CreditSale` ledger entry for the customer;
- a `SaleCashIn` cash entry when the immediate payment is cash.

The sale, sale lines, allocations, stock movements, payment, credit entry and cash entry are committed together.

## Credit Payment

`SqlCustomerCreditCashStore.RegisterPaymentAsync` records a `CreditPayment` ledger entry. Cash payments also create a `CreditPaymentCashIn` entry. The operation is idempotent by `OperationId` and refuses payments greater than the derived debt.

## Returns

Returns are separate records and never update the original sale. A return line cannot exceed the quantity sold minus quantities already returned. `RestockApproved` creates an explicit stock increase movement. `NotRestockable` leaves sellable stock unchanged.

Refund methods are:

- `Cash`: creates a negative `RefundCashOut` cash entry.
- `ExternalCardPending`: stores only the fact that the TPE refund is external.
- `CreditReduction`: creates a `RefundImpact` ledger entry for credit sales.

## Suspensions

Suspended sales are stored in SQL with `Open`, `Resumed` or `Abandoned` status and a `rowversion`. They do not change stock before finalization.
