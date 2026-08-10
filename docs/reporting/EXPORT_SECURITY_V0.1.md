# Sécurité des exports V0.1

## Format

Le Sprint 16 fournit uniquement CSV : UTF-8 avec BOM, séparateur point-virgule, en-têtes explicites, dates UTC ISO 8601, dates civiles `yyyy-MM-dd` et décimaux invariants. Aucun package XLSX n’est ajouté.

## Protection tableur

Toute cellule commençant par `=`, `+`, `-` ou `@` reçoit une apostrophe avant échappement CSV. Chaque cellule est entourée de guillemets et les guillemets internes sont doublés. Cette protection concerne aussi les données historiques/snapshots.

## Données exclues

Les exports ne contiennent ni téléphone, email/adresse fournisseur, note client, mot de passe/hash, code de récupération, donnée carte ou chaîne de connexion. Les fournisseurs sont limités au code et au nom ; les clients au référentiel minimal nécessaire au rapport de crédit.

## Autorisation, audit et emplacement

- permission backend : `reports.export`, en plus de `reports.view` nécessaire à la lecture ;
- audit après génération réussie : `report.export.csv` avec type de rapport et `OperationId` ;
- nom généré : `pharmacy-<rapport>-yyyyMMdd-HHmmss.csv` ;
- écriture déclenchée seulement par l’utilisateur dans `Documents/Logiciel Pharmacie/Exports`.

Une erreur d’accès ou d’écriture est affichée sans masquer l’échec. Aucun export automatique, envoi réseau ou donnée réelle n’est introduit.
