# Schéma SQL Security/Audit V0.1

Migration additive : `20260810145717_SecurityRbacAuditPersistenceV01`.

Schéma `security` :

- `UserAccounts` : login normalisé unique, statut, hash, échecs/verrouillage, timestamps, stamp et rowversion ;
- `Roles` et `Permissions` : codes uniques, rowversion et seed de référence minimal ;
- `UserRoles` et `RolePermissions` : relations uniques sans cascade ;
- `RecoveryCodes` : hash unique, dates d’émission/expiration/usage ;
- `AuthenticationEvents` : succès/échec, login tenté haché, poste haché, raison neutre et corrélation.

Schéma `audit` :

- `AuditEvents` : en-tête append-only applicatif ;
- `AuditEventDetails` : valeurs anciennes/nouvelles uniquement sous forme expurgée lorsqu’un détail est nécessaire.

Toutes les dates sont UTC (`datetime2(7)`), les clés sont `bigint`, les relations historiques sont `NO ACTION`, et les entités mutables User/Role/Permission utilisent `rowversion`.

Scripts : `database/scripts/007_security_rbac_audit_v01_idempotent.sql` prépare seulement les schémas ; `database/verification/verify_sprint15_security_rbac_audit.sql` est strictement en lecture seule.
