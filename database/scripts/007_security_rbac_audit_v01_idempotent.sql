/*
Sprint 15 idempotent schema guard.
EF migration 20260810145717_SecurityRbacAuditPersistenceV01 is the source of truth.
No account, password hash, recovery code or operational data is created here.
*/

IF SCHEMA_ID(N'security') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA security;');
END;

IF SCHEMA_ID(N'audit') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA audit;');
END;

/* The controlled owner bootstrap is performed interactively by the Desktop UI. */
