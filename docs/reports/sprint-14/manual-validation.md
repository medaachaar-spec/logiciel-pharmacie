# Sprint 14 Manual Validation

Status: MANUAL VALIDATION PASS

The complete checklist below was confirmed successful by the project owner on 2026-08-10.

Use a SQL Server development database with Sprint 14 migration applied.

1. Create a client without credit authorization.
2. Attempt a POS sale with credit for that client and verify refusal.
3. Enable credit authorization on the client.
4. Finalize a sale partially paid now and partially credited.
5. Verify stock decremented through FEFO allocations.
6. Verify customer credit balance in the Clients page.
7. Register a partial cash credit payment.
8. Restart the app and verify the debt persists.
9. Suspend a sale.
10. Restart the app and verify persistent suspended sale visibility.
11. Resume or abandon the suspension deliberately.
12. Open a persisted sale and create a partial return with `RestockApproved`.
13. Verify stock increases only for the explicitly restocked quantity.
14. Create a partial return with `NotRestockable`.
15. Verify sellable stock is unchanged.
16. Verify cash refund entries for cash refunds.
17. Attempt a return quantity greater than remaining returnable quantity and verify refusal.
18. Repeat the same return operation id and verify idempotence.
19. Stop SQL Server and verify the Desktop reports SQL unavailable without stack traces.
20. In SSMS, run `database/verification/verify_sprint14_cash_credit_returns.sql`.

Capture requested:

- customer record;
- POS payment with immediate amount and credit amount;
- credit ledger/history;
- persistent suspension list;
- return workflow;
- stock after restock return;
- sale/return history;
- SSMS verification output.
