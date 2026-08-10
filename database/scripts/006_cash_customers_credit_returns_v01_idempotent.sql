/*
Sprint 14 idempotent schema guard.
Use EF migration 20260810134347_CashCustomersCreditReturnsPersistenceV01 as the source of truth.
This script creates schemas when an operator wants to inspect prerequisites before applying migrations.
*/

IF SCHEMA_ID(N'customers') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA customers;');
END;

IF SCHEMA_ID(N'cash') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA cash;');
END;

IF SCHEMA_ID(N'sales') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA sales;');
END;
