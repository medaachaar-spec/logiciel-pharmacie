# Activité des employés V0.1

## Objectif et limites

EMP-02 fournit une attribution fiable des opérations à un compte utilisateur, un historique de sessions applicatives et des vues d'activité par employé. La fonctionnalité ne mesure ni le temps de travail, ni la présence physique, ni la productivité. Une durée affichée est exclusivement une **durée de session applicative** calculée entre le début et la fin enregistrés d'une session fermée.

Les périodes utilisent un intervalle UTC semi-ouvert `[FromUtc, ToUtc)`. Cette convention évite de compter deux fois une opération située exactement à la frontière de deux périodes.

## Sources d'attribution

| Opération | Source d'acteur retenue | Niveau de confiance |
|---|---|---|
| Connexion réussie | `security.AuthenticationEvents` et `security.UserAccounts.LastLoginAtUtc` | forte |
| Déconnexion / fermeture normale | `security.UserSessions` et événement d'authentification | forte |
| Vente finalisée | `sales.Sales.FinalizedByUserAccountId` | forte, attribution métier explicite |
| Retour de vente | `sales.SaleReturns.PerformedByUserAccountId` | forte, attribution métier explicite |
| Suspension / reprise de vente | audit applicatif corrélé | activité de timeline, pas un KPI de vente |
| Règlement de crédit | audit applicatif réussi et identifiant d'opération | activité de timeline |
| Mouvement de caisse indirect | audit du workflow autorisé | activité de timeline ; le ledger seul n'est pas présenté comme source d'acteur |
| Ajustement / inventaire | audit applicatif réussi | activité de timeline |
| Commande / réception fournisseur | audit applicatif réussi | activité de timeline |
| Modification catalogue | audit applicatif réussi | activité de timeline |

Les KPI de ventes et de retours n'utilisent pas l'audit : ils reposent sur les colonnes d'acteur portées par les agrégats métier. Les anciennes lignes sans acteur restent valides grâce aux colonnes nullables et ne sont attribuées artificiellement à personne.

## Sessions applicatives

Une authentification réussie crée une ligne `security.UserSessions` avec l'état `Open`. Une tentative refusée, un compte désactivé ou verrouillé ne crée aucune session et ne met pas à jour la dernière connexion.

Une déconnexion explicite ferme la session avec `ExplicitLogout`. La fermeture normale de l'application utilise `ApplicationExit`. Si une nouvelle connexion trouve une session encore ouverte pour le même compte, l'ancienne passe à `Interrupted` avec `PreviousSessionDetected` et sans heure de fin inventée. Une panne SQL ne provoque ni faux login, ni session locale de secours.

`LastLoginAtUtc` représente la dernière authentification réussie. Le déverrouillage administratif d'un compte n'est pas une connexion et ne modifie donc pas cette valeur.

Seules les sessions fermées possédant une fin fiable peuvent afficher une durée. Une session ouverte ou interrompue n'est jamais convertie en durée supposée.

## Attribution des ventes et retours

Le finalizer SQL reçoit l'identifiant de l'utilisateur courant et l'enregistre dans `FinalizedByUserAccountId` dans la même transaction que la vente, ses lignes, ses paiements, les allocations FEFO et les mouvements de stock. L'identifiant d'acteur participe aussi à l'empreinte d'idempotence afin qu'un rejeu contradictoire ne réattribue pas une vente.

Le workflow de retour capture de la même manière `PerformedByUserAccountId`. L'employé ayant réalisé le retour peut donc être différent de celui ayant finalisé la vente originale. Les indicateurs de retour sont attribués à l'auteur du retour, jamais automatiquement au vendeur initial.

Exemple sur `[2026-08-21T00:00:00Z, 2026-08-22T00:00:00Z)` :

| Employé | Ventes | Montant vendu | Retours réalisés | Montant remboursé |
|---|---:|---:|---:|---:|
| Ahmed | 3 | 500,00 | 0 | 0,00 |
| Sara | 2 | 300,00 | 1 | 50,00 |

Le total global reste 5 ventes et 800,00. L'ajout de l'attribution ne modifie donc aucun KPI historique global. Si Sara retourne une vente créée par Ahmed, le retour est compté dans l'activité de Sara, tandis que la vente reste attribuée à Ahmed.

## Autorisations

Deux permissions complètent le modèle existant :

- `sales.view_all_history` autorise la consultation de l'historique de vente de tous les utilisateurs ;
- `security.view_employee_activity` autorise la vue Manager de synthèse et le détail d'un autre employé.

Un Vendeur conserve `sales.view_history` et peut consulter uniquement ses propres ventes. Il ne peut ni obtenir la synthèse de l'équipe, ni ouvrir le détail d'un collègue, ni contourner cette règle par un identifiant de vente direct. Les contrôles sont appliqués dans les handlers et lecteurs SQL, pas uniquement dans l'interface.

Un Manager reçoit les permissions de vue globale et d'activité. Un compte désactivé ne peut plus se connecter, mais son historique métier et ses sessions passées restent consultables par un utilisateur autorisé.

## Vue Administration

La liste des employés présente le compte, le rôle, l'état actif, la dernière connexion, le nombre de ventes de la période et leur montant. Le détail propose les périodes Aujourd'hui, 7 jours et 30 jours, puis quatre vues : synthèse, sessions, ventes et activité.

Les agrégats ventes/retours sont exécutés par requêtes groupées indépendantes afin d'éviter une multiplication des lignes par jointure. Le chargement d'une liste d'employés n'exécute pas une requête par employé.

La timeline fusionne des projections minimales et déterministes : événements d'authentification réussis, ventes, retours et opérations d'audit réussies pertinentes. Elle expose une date UTC, une catégorie, une action lisible, un résultat et une référence métier lorsque celle-ci est sûre. Elle n'expose ni mot de passe, ni hash, ni recovery code, ni stack trace, ni payload d'audit brut, ni identifiant de corrélation interne.

Exemple de projection :

```text
2026-08-21 10:32 UTC | Vente | Vente finalisée | Succès | V-20260821-0042
```

## Migration

`EmployeeActivityPersistenceV01` crée `security.UserSessions`, ajoute les deux colonnes d'acteur nullables et ajoute les permissions EMP-02. Les relations vers `security.UserAccounts` utilisent `NoAction` pour préserver l'historique. Les index couvrent les lectures par utilisateur et période. Aucune migration antérieure n'est modifiée.
