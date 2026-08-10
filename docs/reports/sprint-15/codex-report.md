# Sprint 15 — Rapport Codex

Statut : **IMPLEMENTED — SQL SECURITY/RBAC/AUDIT VALIDATION PASS — MANUAL SECURITY REVIEW REQUIRED**

## Implémentation

- Domain/Application : règles de mot de passe, session mémoire, horloge injectable, permission authorizer deny-by-default, modèles/contrats sécurité et audit.
- Infrastructure : PasswordHasher Identity V3 (210 000 itérations), bootstrap transactionnel, verrouillage 5/5, recovery codes hashés à usage unique, administration, audit viewer et writer.
- EF/SQL : migration `SecurityRbacAuditPersistenceV01`, 7 tables `security`, 2 tables `audit`, seed de référence OWNER + 20 permissions uniquement.
- Desktop : bootstrap/login/récupération, verrouillage manuel et après dix minutes, logout, topbar utilisateur, navigation filtrée, Administration et Audit.
- Backend : contrôles permission/réauthentification sur vente, retour/remboursement, réception, ajustement/finalisation inventaire, prix, clients/crédit et administration.
- Audit : authentification, session, bootstrap, mot de passe, utilisateurs/rôles/récupération et actions métier critiques instrumentées.

## Décisions et limites connues

- La fenêtre de réauthentification est une décision technique V0.1 de cinq minutes.
- Le compte propriétaire partagé du pilote ne permet pas une attribution individuelle entre ses deux utilisateurs humains.
- Seul `OWNER` est seedé. Les rôles limités doivent être configurés avant la validation multi-rôle complète.
- L’append-only est garanti par l’API applicative et l’absence de voie update/delete ; un rôle SQL très privilégié reste techniquement capable d’écrire directement.
- Les audits de sécurité/administration partagent la transaction critique. Les audits métier ajoutés au Sprint 15 sont écrits immédiatement après succès, dans une transaction séparée V0.1.

## Validation automatisée

- Debug : build réussi, 0 avertissement, 0 erreur ; 350/350 tests PASS.
- Release : build réussi, 0 avertissement, 0 erreur ; 350/350 tests PASS.
- SQL réel : bootstrap, hash, lockout, RBAC, récupération à usage unique, audit et migration validés sur une base temporaire dédiée dans chaque configuration ; nettoyage assuré en `finally`.
- Migration locale : Sprint 15 appliqué à `Pharmacy_Development` ; 7 tables `security`, 2 tables `audit`, 20 permissions et 0 utilisateur avant bootstrap.
- Pending model changes : aucun ; migration Sprint 15 présente dans l’historique local.
- Smokes Desktop : PASS avec SQL sur base temporaire migrée ; PASS sans variable SQL avec erreur contrôlée ; aucun processus résiduel.
- Secrets/PII : scan sources/tests/docs et changements PASS ; aucun credential, token, clé privée, secret loggé ou chaîne locale sensible détecté.
- Git : à la clôture, branche/HEAD initiaux conservés, index vide, aucun remote, aucun commit/push ; modifications laissées visibles pour revue.

## Revue manuelle

Le protocole détaillé est dans `manual-validation.md`. La revue sécurité humaine reste obligatoire avant tout usage réel.
