# Sprint 14 Codex Report

Status: IMPLEMENTED — SQL CASH/CREDIT/RETURNS VALIDATION PASS — MANUAL REVIEW PASS

## Implemented

- Minimal Customers module with explicit credit authorization and rowversion.
- Immutable customer credit ledger.
- Partial or full credit support in the SQL sale finalizer.
- Cash journal for sale cash-in, credit payment cash-in and cash refund out.
- Persistent suspended sale schema and SQL store.
- Return schema and SQL workflow attached to original persisted sales.
- Desktop Customers page.
- POS payment panel fields for customer id and credited amount.

## Migration

- `20260810134347_CashCustomersCreditReturnsPersistenceV01`

## Tables

- `customers.Customers`
- `customers.CustomerLedgerEntries`
- `cash.CashLedgerEntries`
- `sales.SuspendedSales`
- `sales.SuspendedSaleLines`
- `sales.SaleReturns`
- `sales.SaleReturnLines`

Modified:

- `sales.Sales`: `CreditTotal`, `CustomerId`, and total coherence check.

## Validation

- Debug build: PASS, 0 warning, 0 error.
- Release build: PASS, 0 warning, 0 error.
- Debug tests: 341/341 PASS, including the 5 real SQL Server suites.
- Release tests: 341/341 PASS, including the 5 real SQL Server suites.
- EF pending model changes: PASS, no model change since Sprint 14 migration.
- `git diff --check`: PASS, only the existing line-ending warning on the EF model snapshot.
- Secrets scan: no active credential found; matches are rowversion/CSS token terms and documentation references.
- Real SQL execution: 5/5 PASS against the local SQL Server Developer instance, using isolated temporary databases that were removed after execution.

## Out Of Scope

- Mandatory cash opening or closing.
- Cash count and discrepancies.
- Credit ceiling or scoring.
- Card payment integration or card data storage.
- Credit notes, exchanges and wallet.
- Clinical data.

## Manual Review

Manual validation was confirmed successful on 2026-08-10. See `docs/reports/sprint-14/manual-validation.md`.
