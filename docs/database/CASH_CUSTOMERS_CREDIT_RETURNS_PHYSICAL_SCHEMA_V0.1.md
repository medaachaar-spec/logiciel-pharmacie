# Cash, Customers, Credit and Returns Physical Schema V0.1

Sprint 14 adds three production-oriented persistence areas:

- `customers.Customers` for minimal customer records with explicit `CreditAllowed` and `rowversion`.
- `customers.CustomerLedgerEntries` as the immutable credit ledger.
- `cash.CashLedgerEntries` as the immutable cash flow journal.
- `sales.SuspendedSales` and `sales.SuspendedSaleLines` for persistent suspended carts.
- `sales.SaleReturns` and `sales.SaleReturnLines` for returns attached to an original persisted sale.

Credit balances are derived from ledger entries. No credit ceiling, scoring, interest, opening cash session, closing count, card data, wallet, exchange or credit note is introduced.

The Sprint 14 migration is:

`20260810134347_CashCustomersCreditReturnsPersistenceV01`

It also extends `sales.Sales` with:

- `CreditTotal decimal(19,4) NOT NULL DEFAULT 0`
- `CustomerId bigint NULL`

The sales invariant is `PaidTotal + CreditTotal = GrandTotal`. When `CreditTotal > 0`, `CustomerId` is mandatory.
