# Contrôle d’accès des employés V0.1

## Modèle retenu

Le produit présente deux profils prédéfinis : **Manager** (`MANAGER`) et **Vendeur** (`SELLER`). Le rôle `OWNER` reste un mécanisme technique interne, créé uniquement par le bootstrap initial et non attribuable depuis l’administration. Dans l’interface, le compte OWNER est présenté comme un Manager ; le code OWNER n’apparaît ni dans les choix d’affectation, ni dans la barre supérieure, ni dans la matrice courante.

Les rôles personnalisés existants restent disponibles. Plusieurs rôles sont supportés par le backend ; l’interface courante affecte un profil principal à la fois aux comptes non techniques.

## Matrice réelle des modules

| Module | Permission lecture | Permission écriture / action |
|---|---|---|
| Accueil | aucune permission globale ; raccourcis filtrés individuellement | permission du raccourci cible |
| Vente | `sales.view_history` | `sales.create`, `sales.return`, `sales.refund` |
| Produits | `catalog.view` | `catalog.manage`, `catalog.change_price` |
| Stock | `inventory.view` | `inventory.adjust`, `inventory.finalize_count` |
| Achats | `purchasing.view` | `purchasing.receive` |
| Fournisseurs | `purchasing.view` | `purchasing.receive` |
| Clients | `customers.view` | `customers.manage`, `customers.manage_credit` |
| Rapports | `reports.view` | `reports.export`, `reports.manage_settings` |
| Administration | `security.manage_users` | `security.manage_users`, `security.manage_roles` |
| Employés | `security.manage_users` | `security.manage_users`, `security.manage_roles` |
| Sauvegardes | `backup.view` | `backup.create`, `backup.restore` |
| Diagnostic | `devices.configure` | `devices.configure` |

Le module Opérations réutilise aussi `import.view`, `import.execute` et `devices.configure`. Le journal d’activité utilise `audit.view`. La caisse utilise `cash.view`.

## Presets

| Rôle | Permissions principales | Restrictions |
|---|---|---|
| Manager | toutes les permissions métier, gestion comptes/rôles, audit, rapports, branding via `security.manage_users`, sauvegarde, import et périphériques | `backup.restore` n’est pas accordée automatiquement ; OWNER reste inaccessible |
| Vendeur | `sales.create`, `sales.view_history`, `catalog.view`, `inventory.view`, `customers.view` | aucun achat, retour/remboursement, réglage stock, rapport, administration, sauvegarde ou import |

Deux Managers utilisent toujours deux `security.UserAccounts` distincts, donc deux identités, secrets et pistes d’audit distincts.

## Permissions personnalisées

`security.UserPermissionOverrides` ne stocke que les exceptions : utilisateur, permission, effet `Allow` ou `Deny`, auteur/date et rowversion. La contrainte unique `(UserAccountId, PermissionId)` empêche les doublons. Les relations sont sans cascade.

La résolution centralisée applique :

```text
permissions des rôles actifs
+ Allow utilisateur
- Deny utilisateur
= permissions effectives
```

`Deny` gagne toujours, y compris si un `Allow` et un grant de rôle sont présents. La remise à zéro supprime explicitement les exceptions et restaure le comportement du rôle. La sauvegarde des exceptions touche aussi la rowversion et le `SecurityStamp` du compte ; deux modifications concurrentes ne peuvent donc pas s’écraser silencieusement.

## Navigation et sécurité

La sidebar et les raccourcis Accueil filtrent les éléments interdits au lieu de les griser. `AuthorizedRouteView` vérifie la permission avant de créer la page, ce qui empêche une route directe d’amorcer le chargement de données. Les services applicatifs/infrastructure conservent leurs contrôles backend : manipuler le HTML ne confère aucune autorisation.

Exemple Vendeur par défaut : Accueil, Ventes, Produits, Stock et Clients. Les entrées Achats, Rapports, Administration, Sauvegardes et Audit sont absentes.

Exemple Manager : tous les modules métier et d’administration correspondant au preset sont visibles ; une restauration reste soumise à une autorisation supplémentaire.

## OWNER et sessions

- OWNER est créé exclusivement par le bootstrap transactionnel existant.
- Un compte ordinaire ne peut ni recevoir ni retirer OWNER via les contrats d’administration.
- Le dernier OWNER actif ne peut pas être désactivé et ses permissions d’administration essentielles ne peuvent pas être refusées par override.
- Les rôles système sont en lecture seule ; les rôles personnalisés restent modifiables avec rowversion.
- Les permissions sont chargées lors de l’authentification. Une modification est garantie effective à la prochaine connexion/reconstruction de session ; aucun mécanisme temps réel multi-processus n’est introduit en EMP-01.
- Un compte désactivé ne peut pas établir une nouvelle session. Une session explicitement invalidée/fermée ne récupère aucune navigation sans nouvelle authentification valide.

## Audit

La création de compte consigne les rôles attribués. Les changements de rôle et d’overrides consignent l’avant/après redacted, l’acteur, la cible, le résultat, l’horodatage UTC et l’opération. Aucun mot de passe, hash ou code de récupération brut n’est enregistré.
