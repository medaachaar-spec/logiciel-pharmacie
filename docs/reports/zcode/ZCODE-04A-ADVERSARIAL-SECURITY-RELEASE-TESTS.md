# ZCODE-04A — ADVERSARIAL SECURITY & RELEASE TESTS

Date: 2026-08-22
Role: independent adversarial test engineer
Scope: executable behavioral tests converting ZCODE-01 findings against the validated baseline.

## 1. Baseline

- Repository: `C:\Users\HP\Desktop\Logiciel pharmacie-zcode-tests`
- Branch: `zcode/adversarial-tests`
- HEAD: `c08b387f699fb6d6b15b21b0c7aef2e2e59da9aa` (verified exact)
- Initial Git state: clean (`git status --short` empty, `git diff --check` clean)
- Existing baseline tests: **847/848 PASS** at HEAD before this task (unit 176/176,
  integration 671/672). The single pre-existing red is
  `CustomUxCleanupTests.CUSTOM011_NoBusinessHandlerOrServiceWasChangedForUxCopy`
  — its allowlist was not extended for the Configuration/receipt-printing files added
  by the validated baseline commit `c08b387` itself. Classification:
  **EXISTING TEST DEFECT (stale allowlist), not a product defect, not caused by this
  task, left untouched.**
- SQL test target: local default instance `localhost` (MSSQLSERVER), Windows integrated
  auth, provided per-process via `PHARMACY_TEST_SQLSERVER_CONNECTION` (the documented,
  never-versioned mechanism). Backup tests additionally use a shared temp directory via
  `PHARMACY_BACKUP_LOCAL_DIRECTORY` (ACL granted to `NT Service\MSSQLSERVER`), matching
  the documented Sprint 17 environment contract.
- Pilot safety: the pilot instance `.\PHARMACY` (service `MSSQL$PHARMACY`, database
  `Pharmacy`) was **never contacted**. A real `C:\ProgramData\LogicielPharmacie\config\pilot.json`
  exists on this machine and was only *read* indirectly by the resolver tests through a
  synthetic override pointing at a non-existent server. `Pharmacy_Development` was never
  used for mutation. All SQL tests create/drop disposable databases named
  `Pharmacy_ZC04_Test_<12-hex>` from `master`, guarded by regex `^Pharmacy_ZC04_Test_[0-9a-f]{12}$`
  plus an asserted prefix on drop. Post-run verification: zero leftover disposable
  databases on the test server.
- Fixture safety guards proved before execution (repository's existing infrastructure,
  mirrored by `Zc04TestHarness`): name-regex validation on create AND drop,
  `InitialCatalog=master` server connection only, `Pooling=false`, `ClearAllPools()`
  before drop, `SINGLE_USER WITH ROLLBACK IMMEDIATE` on drop.

## 2. Test files created/modified

Created (all under `tests/`, untracked, nothing staged or committed):

1. `tests/Pharmacy.IntegrationTests/Zc04TestHarness.cs` — shared disposable-SQL harness,
   clocks, allow-all seeding authorizer, `SeededSaleFactory` (real full sale chain).
2. `tests/Pharmacy.IntegrationTests/Zc04AdversarialSecurityTests.cs` — ZC04-A01, A01B, A02, A07A, A07B.
3. `tests/Pharmacy.IntegrationTests/Zc04PurchasingAuthorizationTests.cs` — ZC04-A03A–A03E.
4. `tests/Pharmacy.IntegrationTests/Zc04ReportingCsvExportTests.cs` — ZC04-A04.
5. `tests/Pharmacy.IntegrationTests/Zc04ConnectionResolverTests.cs` — ZC04-A05A–A05E (DB-independent).
6. `tests/Pharmacy.IntegrationTests/Zc04BackupOperationIdReplayTests.cs` — ZC04-A06.
7. `tests/Pharmacy.IntegrationTests/Zc04PostCommitAuditFailureTests.cs` — ZC04-A08A–A08C.
8. `tests/Pharmacy.IntegrationTests/Zc04BusinessDayTests.cs` — ZC04-A09.
9. `tests/Pharmacy.IntegrationTests/Zc04ConcurrencyInvariantTests.cs` — ZC04-A10A–A10D.

Production source modifications: **NONE** (`git status` shows only the nine untracked
test files; `src/`, `database/`, `installer/`, `scripts/` untouched).

## 3. Findings reproduction summary

| Test ID | ZCODE-01 finding | Result | Severity | Short evidence |
|---|---|---|---|---|
| ZC04-A01 | ZC-001 self-permission escalation | REPRODUCED | BLOCKER | Manager (security.manage_roles + recent reauth) called the real `SetUserPermissionOverridesAsync` on own account; Allow `backup.restore` accepted, present in returned effective permissions |
| ZC04-A01B | ZC-001 escalation durability | REPRODUCED | BLOCKER | Persisted Allow override grants `backup.restore` at next real login (`Assert.DoesNotContain` failed; set contains `backup.restore`) |
| ZC04-A02 | ZC-002A disabled live session | REPRODUCED | BLOCKER | Seller disabled by independent admin session still finalized a real sale (`VTE-20260822-36237F…`, `sales.Sales=1`); new logins correctly rejected |
| ZC04-A03A | ZC-003 purchasing reads | REPRODUCED | HIGH | Seller without `purchasing.view` listed real purchase orders (1 order `ZC04-ORDER-1`) via production-wired `PurchasingQueryHandlers` |
| ZC04-A03B | ZC-003 order details | REPRODUCED | HIGH | Unauthorized seller read PO details (status PartiallyReceived, lines) and supplier details |
| ZC04-A03C | ZC-003 suppliers/receipts | REPRODUCED | HIGH | Unauthorized seller listed suppliers (1 row); receipts/products reads share the same gap |
| ZC04-A03D | ZC-003 receipt mutation | NOT REPRODUCED | — | `SecurityCommandGuard` denies all purchasing commands without `purchasing.receive`; SQL unchanged |
| ZC04-A03E | control | NOT REPRODUCED (control) | — | Owner with purchasing permissions retains access |
| ZC04-A04 | ZC-004 CSV >200 | REPRODUCED | HIGH | 251 qualifying sales → summary 251, snapshot 200, CSV 200 data rows, success, no truncation signal |
| ZC04-A05A | ZC-007 resolver dev case | NOT REPRODUCED | — | `Database=Pharmacy_Development` returned as-is |
| ZC04-A05B | ZC-007 staging fallthrough | REPRODUCED | HIGH | Explicit `Pharmacy_Staging` env target silently ignored; resolver returned pilot DB (synthetic `ZC04-SYNTHETIC-SRV`, `Pharmacy`) |
| ZC04-A05C | ZC-007 malformed fallthrough | REPRODUCED | HIGH | Malformed env connection silently ignored; pilot DB returned |
| ZC04-A05D | ZC-007 disallowed target | REPRODUCED | HIGH | Explicit `prod-sql-01/Pharmacy_Prod` silently ignored; pilot DB returned |
| ZC04-A05E | ZC-007 no-env fallback | NOT REPRODUCED | — | No env connection → documented pilot-config fallback (or null); correct behavior |
| ZC04-A06 | ZC-011 backup OperationId replay | REPRODUCED | MEDIUM | Retry of successful backup with same OperationId → `backup.operation.failed` «La sauvegarde n’a pas abouti» while DB keeps 1 Completed record and 1 file on disk |
| ZC04-A07A | ZC-013 disabled password change | REPRODUCED (behavior recorded) | MEDIUM | Disabled account's open session changed its own password with correct current password (PasswordChange events: 1); not auto-classified release-blocking per task §11 |
| ZC04-A07B | ZC-013 wrong-current-password | REPRODUCED (hardening) | MEDIUM | 12 wrong-current-password attempts: FailedLoginCount 0, no lockout, 0 failure events, 0 audit failures — no mitigation at all |
| ZC04-A08A | ZC-017 silent audit loss | REPRODUCED | HIGH | Audit writer returning `security.database_unavailable` post-commit: sale committed + handler SUCCESS + no `sale.finalize` audit row (swallowed by `BusinessAuditRecorder`) |
| ZC04-A08B | ZC-017 retry risk | REPRODUCED | HIGH | Audit writer throwing post-commit: caller received exception while `sales.Sales=1`, `Payments=1`, Sale movements=1 — operator would retry a committed financial transaction |
| ZC04-A08C | control | NOT REPRODUCED (control) | — | Real `SqlAuditStore` writer: sale + audit row both present |
| ZC04-A09 | ZC-019 business day | POLICY REQUIRED | MEDIUM | No business-timezone policy exists anywhere in repo configuration; dashboard "Aujourd’hui" window = [UTC midnight, UTC midnight+1); sale at 00:30 local (+01:00 assumed) attributed to previous business day |
| ZC04-A10A | concurrency over-receipt | NOT REPRODUCED | — | 7+5 vs 10 ordered: ReceivedQuantity ≤ 10, receipt lines == received, invariant preserved |
| ZC04-A10B | concurrency over-return | NOT REPRODUCED | — | 7+5 vs 10 sold: returned ≤ 10, refunds == returned × unit price |
| ZC04-A10C | sale vs inventory stock | NOT REPRODUCED | — | afterPlan-hooked deterministic interleaving: exactly one operation succeeds; balance 4 or 2, never negative, never lost update |
| ZC04-A10D | receipt vs inventory finalize | NOT REPRODUCED | — | Barrier-synchronized: final balance ∈ {4, 24, 30} matching reported outcomes exactly |

## 4. ZC-001 — Self privilege escalation

- Test: `ZC04A01_NonOwnerAdministratorMustNotEscalateOwnPermissionsViaSelfTargetedOverrides` (+ companion `ZC04A01B`).
- Setup: real SQL DB (13 migrations); OWNER bootstrap; MANAGER-equivalent role =
  `SecurityRolePresets.ManagerPermissions` (29 permissions, excludes `backup.restore`);
  manager user + isolated session; legitimate `ReauthenticateAsync` (recent-reauth satisfied);
  manager-scoped `SqlSecurityAdministrationService`.
- Actual result: `SetUserPermissionOverridesAsync(self, Allow backup.restore)` **succeeded**.
- Effective permissions after attempt: returned view contains `backup.restore`
  (`security.authorization.denied` no longer returned by the manager's authorizer).
- Persisted overrides: row inserted in `security.UserPermissionOverrides` with
  `Effect='Allow'` (verified via SQL in companion); audit event `user.permission_overrides.set` written.
- Next login (`ZC04A01B`): session permissions **include `backup.restore`** — the escalation
  is durable. (Companion also proves the reverse: a Deny override on another user persists correctly.)
- Control: the same manager administration changing ANOTHER user's overrides works
  (Deny `backup.restore` on a third party accepted, persisted, audited) — existing rules intact.
- Verdict: **REPRODUCED — BLOCKER.** `SetUserActiveAsync` and `SetUserRolesAsync` carry
  self-targeting guards; `SetUserPermissionOverridesAsync` has none.

## 5. ZC-002A — Disabled live session

- Test: `ZC04A02_DisabledLiveSessionMustNotPerformProtectedBusinessOperations`.
- Existing session: kept open after independent admin disablement; real full sale
  finalization through `FinalizeSaleHandler` (draft → preparation → SqlSaleFinalizer)
  **succeeded** — sale reference `VTE-20260822-36237FDD0AE3490DBE1ECAFFF88D8095`,
  `sales.Sales=1`, payment + FEFO allocation + `Sale` stock movement persisted.
- New login: correctly **rejected** (login re-reads `IsActive`).
- Business mutation: committed from the disabled session (SQL verified).
- Verdict: **REPRODUCED — BLOCKER.** `PermissionAuthorizer` validates only the in-memory
  session snapshot; no handler re-checks account state. A disabled user can operate
  indefinitely until they log out.

## 6. ZC-003 — Purchasing backend authorization

Handlers wired exactly like production DI (`SalesServiceCollectionExtensions`):
`PurchasingQueryHandlers(new SqlPurchasingReader(factory))`, commands with real authorizer.

| Handler/operation | Required permission | Unauthorized result | SQL changed |
|---|---|---|---|
| `ListOrdersAsync` | purchasing.view | **Success + real data (1 PO)** | read-only |
| `GetOrderAsync` | purchasing.view | **Success + details (status, lines)** | read-only |
| `GetSupplierAsync` | purchasing.view | **Success + supplier data** | read-only |
| `SearchSuppliersAsync` | purchasing.view | **Success + supplier list** | read-only |
| `ListReceiptsAsync` | purchasing.view | **Success + receipts** | read-only |
| `GetReceiptAsync` | purchasing.view | **Success + receipt details** | read-only |
| `SearchProductsAsync` | purchasing.view | **Success + purchasable products** | read-only |
| `GetDiagnosticsAsync` | purchasing.view | same gap (same handler) | read-only |
| `CreateSupplierAsync` | purchasing.receive (guard) | denied `security.authorization.denied` | no |
| `CreateOrderAsync` | purchasing.receive (guard) | denied | no |
| `FinalizeReceiptAsync` | purchasing.receive + recent reauth (guard) | denied | no |

Verdict: **REPRODUCED (reads) — HIGH.** `PurchasingQueryHandlers` accepts no authorizer
at all (single ctor parameter: the reader). UI-hidden ≠ backend-denied for every
purchasing read. Mutations are correctly guarded (NOT REPRODUCED).

## 7. ZC-004 — CSV completeness

- Seed count: 251 finalized sales (finalizer-shaped rows: SaleOperations Processing→Completed,
  `Status='Completed'`, one per OperationId) inside one report period, disposable SQL DB.
- Qualifying count: 251 (SQL COUNT within period).
- Reported count: `ExecutiveSummary.SaleCount = 251` (summary query has no Take).
- CSV rows: **200** data rows (header excluded, UTF-8 BOM handled).
- Truncation warning: **none** — `CsvExportView` carries only FileName/ContentType/Content;
  the desktop `ReportingCoordinator` exports with `new ReportingQuery(period)` whose
  default `Limit = 200` is silently inherited by `GetSnapshotAsync(...).Take(query.Limit)`.
- Verdict: **REPRODUCED — HIGH** (silent 251 → 200 with success).

## 8. ZC-007 — Resolver

| Case | Input | Actual behavior | Resolved target | Verdict |
|---|---|---|---|---|
| A | `Server=localhost;Database=Pharmacy_Development` | returned as-is | Pharmacy_Development | NOT REPRODUCED |
| B | `Server=localhost;Database=Pharmacy_Staging` | silently ignored; pilot config loaded | pilot DB (synthetic srv, `Pharmacy`) | REPRODUCED |
| C | malformed string | SqlConnectionStringBuilder ArgumentException swallowed; pilot config loaded | pilot DB | REPRODUCED |
| D | `Server=prod-sql-01;Database=Pharmacy_Prod` + pilot config exists | silently ignored; pilot config loaded | pilot DB | REPRODUCED |
| E | no env connection | documented fallback to configured pilot (or null) | pilot config / null | NOT REPRODUCED (intended) |

No connection was ever opened; the pilot target in tests is a synthetic non-existent
server. Note: this machine really has `C:\ProgramData\LogicielPharmacie\config\pilot.json`
pointing at `.\PHARMACY` — the reproduced fallthrough targets the live pilot here.
Verdict: **REPRODUCED (B/C/D) — HIGH** (only exact `Database=Pharmacy_Development` is
honored; every other explicit target silently becomes the pilot DB).

## 9. ZC-011 — Backup replay

- OperationId: single GUID reused for both calls; disposable DB + temp backup directory.
- First call: success, `VerifyOnlyPassed`, SHA-256 present, file on disk.
- Second call (same OperationId, same request): **failure** `backup.operation.failed`
  («La sauvegarde n’a pas abouti ; aucune réussite n’a été déclarée») — generic failure.
- Backup records: 1 row total for the OperationId, Status `Completed`, 0 `Failed`
  (the retry's duplicate insert violates `UX_operations_BackupRecords_OperationId`;
  the retry's failure bookkeeping also silently fails).
- Files: 1 `.bak` on disk (no duplicate write; cleaned up after test).
- Verdict: **REPRODUCED — MEDIUM** (misleading failure for a legitimate retry of a
  successful operation; contrast with sales/receipts which resolve replays idempotently).

## 10. ZC-013 — Password behavior

- Disabled account: open session `ChangePasswordAsync` with CORRECT current password →
  **success** (PasswordChange event = 1, hash replaced). No `IsActive` check exists in
  `ChangePasswordAsync`. Recorded per task §11 — not auto-classified release-blocking.
- Wrong current password attempts: 12 attempts → 12 `invalid_credentials` failures;
  `FailedLoginCount=0`, `LockedUntilUtc=NULL`, `AuthenticationEvents` failure rows = 0,
  audit failures = 0, no throttling — the ChangePassword surface permits unlimited
  unlogged password guessing (unlike Login, which counts/locks/audits).
- Audit: none for failed attempts (early return before any event write).
- Lockout/throttle: none on this surface.
- Verdict: **REPRODUCED — MEDIUM / SECURITY HARDENING OPPORTUNITY** (A: disabled-account
  credential mutation; B: unthrottled unaudited guessing).

## 11. ZC-017 — Post-commit audit failure

Fault injection at the `IAuditWriter` seam (test-local adapters around the real
`BusinessAuditRecorder` + real `SqlSaleFinalizer`); no production changes.

- Business committed: YES in both scenarios (`sales.Sales=1`, `Payments=1`,
  Sale stock movement=1).
- Audit: A) writer returns `security.database_unavailable` (exactly what
  `SqlAuditStore.AppendAsync` returns on DB failure) → **silently swallowed** (the
  recorder ignores the `Result`); B) writer throws a non-SQL exception (not caught by
  the store) → **propagates to the caller**.
- Returned result: A) SUCCESS (with lost audit trail); B) **exception → caller sees failure**.
- Retry risk: A) none signaled but audit trail lost; B) **YES — operator would retry an
  already-committed financial transaction** (the exact ZCODE-01 hypothesis).
- Control (A08C): real writer → success + audit row both present.
- Verdict: **REPRODUCED (both variants) — HIGH.** The original audit hypothesis
  ("caller receives overall failure") is confirmed for the throwing path; the
  DB-unavailable path is worse in a different way (silent audit loss on SUCCESS).

## 12. ZC-019 — Business day

- Existing timezone policy: **none** (searched src/, docs/configuration, docs/reporting,
  docs/application for business/pharmacy/local timezone policy — no definition exists;
  `SqlDashboardService` computes `todayUtc = clock.UtcNow.Date`).
- UTC timestamps: Sale A `2026-08-21T23:30Z`, Sale B `2026-08-22T11:00Z`.
- Assumed/defined local time: UTC+01:00 (Africa/Casablanca, explicitly labeled as
  ASSUMED for the demonstration; the app defines nothing).
- Expected business date (local-calendar reading): both sales belong to local
  2026-08-22; Sale A is 2026-08-22 00:30 local.
- Dashboard result: `TodaySaleCount = 1` (only Sale B); Sale A excluded from
  «Aujourd’hui» despite same local business date — the window is
  [UTC midnight, UTC midnight+1).
- Verdict: **POLICY REQUIRED** — UTC persistence is correct; the business-day boundary
  for «Aujourd’hui» labels needs an explicit timezone policy decision (test documents
  current behavior deterministically; no production timezone invented).

## 13. Concurrency

| Scenario | Synchronization | Outcome | Invariant preserved |
|---|---|---|---|
| A10A PO over-receipt (10 ordered; 7+5) | AsyncBarrier(2) at both starts; Serializable txn + required order rowversion | received ≤ 10; receipt-line sum == received; ≥1 success iff state changed | YES |
| A10B concurrent returns (10 sold; 7+5) | AsyncBarrier(2); Serializable txn + replay resolution | returned ≤ 10; refunds == returned × 10 MAD | YES |
| A10C sale finalize vs manual decrease (stock 10; 6 vs 8) | repository `afterPlan` hook parks the finalizer INSIDE its Serializable txn after the stock read; TaskCompletionSource release; 90s watchdogs | exactly one succeeds; balance 4 (sale won) or 2 (decrease won); never negative; never both-success | YES |
| A10D goods receipt (+20) vs inventory finalize (count 4/10) | AsyncBarrier(2) at both starts; both Serializable | balance ∈ {4, 24, 30} exactly matching reported outcomes; inventory-finalize conflicts via UpdatedAtUtc/rowversion | YES |

No HARNESS REQUIRED: the existing `SqlSaleFinalizer.afterPlan` internal hook and the
serializable transactions provided deterministic-enough coordination for all four.

## 14. Extra findings

Same-family inspection of neighboring query handlers while testing purchasing:
`CustomerQueryHandlers`, `InventoryQueryHandlers`, `GetFinalizedSalesHandlers`,
`GetSaleReturnPreparationHandler`, `SqlDashboardService`, `SqlEmployeeActivityReader`
all carry and enforce authorizers. **No additional missing-authorization family found** —
the purchasing query handlers are the single gap. No ZC04-EXTRA tests created.

## 15. Existing regressions

| Suite | Result |
|---|---|
| RBSELL001–023 (Catalog/Customers hardening) | GREEN (unit suite 176/176 after baseline repair) |
| UX-03 (UX0301–UX0310) | GREEN |
| PRINT / UX02 / receipt OFF | GREEN |
| EMP (access control + activity) | GREEN |
| SEC | GREEN |
| DASH | GREEN |
| Sales / Returns / Credit / Inventory / Purchasing | GREEN |
| Deployment (Sprint18) | GREEN (14/14) |
| E2E | GREEN |
| CUSTOM011 | PRE-EXISTING RED (stale allowlist vs receipt-printing Configuration files added by baseline commit c08b387; see §1) |

Note: the first full-suite run initially showed 5 backup-related failures caused by the
local SQL Server backup-directory ACL (test process could not read the instance default
backup path). Repaired the LOCAL DISPOSABLE TEST ENVIRONMENT ONLY (shared temp dir with
`NT Service\MSSQLSERVER` modify ACL via `PHARMACY_BACKUP_LOCAL_DIRECTORY`, the documented
environment contract). Pilot and Pharmacy_Development untouched. After repair all
backup-dependent tests pass.

## 16. Final test result

Confirmed by the final full run of both projects (unit 176/176; integration
697 total = 672 existing + 25 new, 16 failed = 1 pre-existing CUSTOM011 + 15
intentional reproduced-defect reds):

- Existing baseline tests: 848 (176 unit + 672 integration); 847 pass,
  1 pre-existing red (CUSTOM011, stale allowlist — see §1 and §15).
- New adversarial tests: **25** (security 5, purchasing 5, CSV 1, resolver 5,
  backup 1, audit 3, business day 1, concurrency 4).
- Passing (new): **10** — ZC04-A03D, A03E, A05A, A05E, A08C, A09, A10A, A10B, A10C, A10D.
- Failing because defect reproduced: **15** — ZC04-A01, A01B, A02, A03A, A03B, A03C,
  A04, A05B, A05C, A05D, A06, A07A, A07B, A08A, A08B.
- Harness failures: 0 (every red carries an explicit «… REPRODUCED …» evidence message;
  no red is a test bug).
- Reproduced-defect tests are intentional artifacts, NOT accidental regressions.

## 17. Recommended Codex fixes (do NOT implement here)

1. **ZC-001 (BLOCKER)** — `SqlSecurityAdministrationService.SetUserPermissionOverridesAsync`
   (`src/Pharmacy.Infrastructure/Security/SqlSecurityAdministrationService.cs:452`):
   reject `request.UserId == session.User.UserId` self-targeted overrides (mirror the
   guards already present in `SetUserActiveAsync`/`SetUserRolesAsync`), and/or cap Allow
   overrides to permissions already granted by the target's roles.
   Failing tests: ZC04-A01, ZC04-A01B. Invariant: a non-OWNER administrator cannot widen
   their own effective permission set, durably or transiently.
2. **ZC-002A (BLOCKER)** — session authorization path (`PermissionAuthorizer` /
   handler pipeline): re-validate account `IsActive` (or SecurityStamp) against the DB on
   protected commands, or invalidate live sessions on disablement.
   Failing test: ZC04-A02. Invariant: a disabled account's open session cannot perform
   protected business operations; only logout should end that ability, not disablement
   being advisory.
3. **ZC-003 (HIGH)** — `PurchasingQueryHandlers`
   (`src/Pharmacy.Application/Purchasing/Queries/PurchasingQueryHandlers.cs`): accept and
   enforce `IPermissionAuthorizer` with `purchasing.view` on every read (production DI in
   `SalesServiceCollectionExtensions` must pass it). Failing tests: ZC04-A03A/B/C.
   Invariant: UI hidden + backend denied.
4. **ZC-004 (HIGH)** — reporting export (`SqlReportingService.ExportCsvAsync` +
   `ReportingCoordinator.Query()`): exports must not silently inherit the snapshot
   Limit=200; either export unbounded (with a sane ceiling) or propagate an explicit
   truncation flag consumed by the UI. Failing test: ZC04-A04. Invariant: success ⇒ all
   qualifying rows, or explicit truncation reporting.
5. **ZC-007 (HIGH)** — `DesktopConnectionResolver.Resolve()`: an explicitly supplied
   env connection that is malformed or targets a database other than
   `Pharmacy_Development` must produce a hard error (or at minimum a surfaced warning),
   never a silent fallthrough to the pilot configuration. Failing tests: ZC04-A05B/C/D.
   Invariant: explicit-but-rejected targets never silently become the writable pilot DB.
6. **ZC-017 (HIGH)** — `BusinessAuditRecorder`/`FinalizeSaleHandler` post-commit audit:
   (a) do not swallow the audit writer's failure Result on a committed business
   operation (surface/queue/log it); (b) ensure a post-commit audit exception cannot
   present the committed operation as failed (catch inside the recorder; the sale is
   already committed and idempotent replay exists — return success with degraded-audit
   signaling). Failing tests: ZC04-A08A/B. Invariant: caller-visible failure ⇒ business
   not committed; success ⇒ audit trail present or escalated.
7. **ZC-011 (MEDIUM)** — `SqlBackupRecoveryService.CreateAsync`: resolve OperationId
   replay idempotently (return the existing Completed record when the fingerprint
   matches), like sales/receipts/imports already do. Failing test: ZC04-A06.
8. **ZC-013 (MEDIUM)** — `SqlAuthenticationService.ChangePasswordAsync`: check
   `user.IsActive`; count/audit failed current-password attempts (reuse the login
   lockout rules). Failing tests: ZC04-A07A/B.
9. **ZC-019 (POLICY REQUIRED)** — decide and configure a business timezone, then make
   the dashboard/reporting «Aujourd’hui» windows use it. Evidence test: ZC04-A09
   (currently green, documents UTC-window behavior).

## 18. Priority ranking

- BLOCKER: ZC-001 (self-escalation, durable), ZC-002A (disabled session keeps selling).
- HIGH: ZC-003 (purchasing data exposure), ZC-004 (silent export truncation),
  ZC-007 (resolver pilot fallthrough), ZC-017 (post-commit audit failure/retry risk).
- MEDIUM: ZC-011 (backup replay UX), ZC-013 (password surface hardening),
  ZC-019 (timezone policy).
- LOW: —.

## 19. Git integrity

- Branch: `zcode/adversarial-tests`
- HEAD: `c08b387f699fb6d6b15b21b0c7aef2e2e59da9aa` (unchanged)
- Changed test/docs files: 9 untracked files under `tests/Pharmacy.IntegrationTests/`
  plus this report under `docs/reports/zcode/`. Nothing staged, nothing committed.
- Production files changed: **NONE**
- `git diff --check`: clean

## 20. Final verdict

**CONFIRMED DEFECTS REQUIRE CODEX FIXES**

(2 BLOCKER + 4 HIGH + 3 MEDIUM reproduced; release-blocking security escalations exist.)

- REPRODUCED: ZC-001 (2 tests), ZC-002A, ZC-003 (3 tests), ZC-004, ZC-007 (3 tests),
  ZC-011, ZC-013 (2 tests), ZC-017 (2 tests) — 15 red tests.
- NOT REPRODUCED: ZC-003 mutations (guard works), ZC-007 dev/no-env cases, concurrency
  A10A–D (invariants hold), audit control A08C.
- PARTIAL: —
- POLICY REQUIRED: ZC-019.
- HARNESS REQUIRED: — (the existing `afterPlan` hook + serializable transactions sufficed).

ZCODE-04A — COMPLETE
