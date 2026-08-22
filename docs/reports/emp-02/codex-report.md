# Rapport EMP-02 — Attribution et activité des employés

## Analyse initiale de l'attribution

| Opération | Acteur avant | Source | Changement requis |
|---|---|---|---|
| Login réussi | fiable | `AuthenticationEvents`, `LastLoginAtUtc` | créer une session persistante uniquement après succès |
| Logout | événement d'authentification, pas de durée persistée | audit/authentication | fermer la session avec une fin exacte |
| Vente finalisée | absent de l'agrégat vente | finalizer SQL | ajouter `FinalizedByUserAccountId` et le capturer avant les appels asynchrones |
| Retour | absent de l'agrégat retour | workflow SQL de retour | ajouter `PerformedByUserAccountId`, distinct du vendeur original |
| Suspension / reprise | présent dans l'audit | audit corrélé | projeter dans la timeline uniquement |
| Règlement de crédit | présent dans l'audit | audit réussi + opération | projeter dans la timeline |
| Mouvement de caisse | ledger sans acteur stable | audit du workflow | ne pas attribuer à partir du ledger seul |
| Inventaire / ajustement | présent dans l'audit | audit réussi | projeter dans la timeline |
| Achat / réception | présent dans l'audit | audit réussi | projeter dans la timeline |
| Catalogue | présent dans l'audit | audit réussi | projeter dans la timeline |

## Architecture EMP-02

- attribution métier : colonnes d'acteur explicites sur `Sales` et `SaleReturns`, écrites dans les transactions existantes ;
- audit timeline : source secondaire pour les activités non agrégées, limitée aux résultats réussis et projetée sans payload sensible ;
- sessions : `security.UserSessions` avec états `Open`, `Closed`, `Interrupted` et raisons de fermeture ;
- permissions : `sales.view_all_history` et `security.view_employee_activity`, contrôlées côté backend ;
- période : intervalle UTC semi-ouvert `[FromUtc, ToUtc)`, validation stricte et ordre déterministe ;
- lecture : agrégats SQL groupés séparés pour éviter N+1 et multiplication par jointure.

## Sessions

- login : une session `Open` est créée uniquement après authentification réussie ; `LastLoginAtUtc` est mis à jour au même moment ;
- logout : fin exacte et raison `ExplicitLogout` ;
- fermeture application : fin exacte et raison `ApplicationExit` en best effort ;
- crash/interruption : une session orpheline devient `Interrupted` lors de la connexion suivante, sans heure de fin fabriquée ;
- durée : libellée exclusivement « Durée de session » et calculée seulement pour une session fermée fiable ;
- refus/lock/disabled/SQL indisponible : aucune session ni faux fallback local ;
- déverrouillage : ne modifie pas la dernière connexion.

## Ventes par utilisateur

Scénario `[2026-08-21T00:00:00Z, 2026-08-22T00:00:00Z)` : Ahmed réalise 3 ventes pour 500,00 ; Sara réalise 2 ventes pour 300,00. Le résumé par employé affiche respectivement `3 / 500,00` et `2 / 300,00`, tandis que le KPI global reste `5 / 800,00`. Les anciennes ventes sans acteur restent non attribuées et les requêtes globales historiques ne changent pas.

L'acteur est inclus dans l'empreinte d'idempotence. Deux finalisations concurrentes conservent leurs identités propres ; le contrat de concurrence stock existant reste inchangé.

## Retours / crédit / autres activités

Le retour porte son propre `PerformedByUserAccountId`. Un retour effectué par Sara sur une vente d'Ahmed est donc une activité de Sara, sans réattribuer la vente d'Ahmed. Les règlements de crédit, suspensions/reprises, opérations de stock, achats/réceptions et changements catalogue utilisent l'audit réussi lorsqu'il constitue une source fiable ; ils alimentent la timeline mais pas les KPI de vente.

## Politique vendeur

Un Vendeur muni de `sales.view_history` peut voir ses propres ventes finalisées et préparer un retour uniquement dans le périmètre autorisé par les règles métier existantes. Sans `sales.view_all_history`, les handlers et lecteurs SQL filtrent sur son `UserAccountId`, y compris pour un accès par identifiant. Il ne peut pas voir les ventes d'un collègue, la synthèse de l'équipe ni le détail d'un autre employé. `security.view_employee_activity` est requis pour la vue inter-employés.

## Vue Manager

La liste Administration affiche compte, rôle, état, dernière connexion, ventes et montant sur la période. Le détail employé fournit Aujourd'hui, 7 jours et 30 jours, des indicateurs ventes/retours, puis les onglets Synthèse, Sessions, Ventes et Activité. L'historique d'un compte désactivé est préservé. Le responsive conserve un tableau scrollable sans masquer les actions.

## Timeline

Projection réelle attendue :

```text
2026-08-21 10:32 UTC | Vente | Vente finalisée | Succès | V-20260821-0042
```

Les colonnes projetées sont limitées à la date UTC, la catégorie, l'action lisible, le résultat et une référence métier sûre. Aucun secret, hash, recovery code, stack trace, payload brut ou identifiant de corrélation interne n'est exposé.

## Fichiers modifiés

- Application Sales : contrats de lecture, requêtes d'historique, finalisation, retour et modèles de requête avec acteur ;
- Application Security : contrats, modèles, erreurs, permissions et présentation ;
- Infrastructure Persistence : entités/configurations Sales et Security, DbContext, snapshot et migration `20260820232920_EmployeeActivityPersistenceV01` ;
- Infrastructure Sales : finalizer, lecteur SQL et store crédit/retour ;
- Infrastructure Security : authentification, support de persistance, lecteur `SqlEmployeeActivityReader` et enregistrement DI ;
- Desktop : fermeture de session à la sortie, coordinator Administration, page Administration et styles Security ;
- Tests : `EmployeeActivityTests.cs` et gardes/régressions concernés ;
- Documentation : `docs/security/EMPLOYEE_ACTIVITY_V0.1.md` et ce rapport.

## Tests EMP-02

| Test | Résultat | Preuve |
|---|---|---|
| EMP031–EMP037 — sessions, login/logout, interruption, dernière connexion | PASS | tests SQL isolés, états et raisons persistés |
| EMP038–EMP040 — acteur vente et concurrence | PASS | acteurs lus depuis `sales.Sales`, isolation concurrente vérifiée |
| EMP041–EMP046 — agrégats employé, périodes, absence de multiplication | PASS | scénarios numériques et requêtes groupées |
| EMP047–EMP051 — retour, crédit et timeline métier | PASS | auteur du retour distinct et sources d'audit filtrées |
| EMP052–EMP058 — permissions Manager/Vendeur et comptes désactivés | PASS | contrôles backend et historique préservé |
| EMP059–EMP065 — UI, secrets, migration et régressions | PASS | gardes structurels, redaction et modèle EF |
| Total EMP031–EMP065 | **35/35 PASS** | Debug ciblé sur `EmployeeActivityTests` |

## EMP-01 / CUSTOM / BRAND / SEC regression

- gardes structurels ciblés : 62/62 PASS ;
- régression SQL Sales ciblée : 1/1 PASS ;
- EMP-01, CUSTOM, BRAND, SEC, Sales/RET/CRD et Reporting : inclus dans les suites globales Debug et Release, toutes vertes ;
- test Reporting rendu indépendant de la date réelle tout en conservant les mêmes règles métier et la même isolation SQL.

## Migration / EF

Migration added: YES

Migration name: `EmployeeActivityPersistenceV01`

Migration count: 12

Pending model changes: NONE

Anciennes migrations modifiées: NONE

## Tests globaux

Debug build: PASS — 0 warning, 0 erreur

Release build: PASS — 0 warning, 0 erreur

Debug tests: 741/741 PASS — 0 ignoré

Release tests: 741/741 PASS — 0 ignoré

Baseline: 706 ; ajout EMP-02: 35 ; total: 741

E2E: PASS — 1 scénario, assertion interne de 27/27 checkpoints uniques

## SQL

Temporary DB: 0 base résiduelle après validation. Les tests utilisent une connexion serveur vers `master`, créent leurs bases temporaires et les détruisent.

Backup temporaire: dossier isolé vide après validation.

Pilot mutation: NONE — `.\PHARMACY / Pharmacy` reste à 9 migrations, sans `security.UserSessions` et sans migration EMP-02 appliquée.

## Git

branch: `customization/employee-activity`

HEAD: `102d8123b0ae8d77ba604e2bba785768cba41275`

status: changements EMP-02 non stagés, aucun artefact `bin/obj` suivi

diff check: PASS

index: vide

remote: `origin` préexistant, non modifié

Aucun staging, commit, push, merge, rebase, tag, ajout de remote ou changement de branche n'a été effectué.

EMP-02 EMPLOYEE ACTIVITY — GATE PASS
