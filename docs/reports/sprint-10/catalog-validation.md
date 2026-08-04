# Sprint 10 — Validation Catalogue SQL et POS

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL CATALOG VALIDATION PASS — MANUAL CATALOG REVIEW REQUIRED`

## Environnement validé

- SQL Server 2025 Developer 17.0.1125.2, instance locale par défaut ;
- base `Pharmacy_Development` ;
- compatibilité 170 ;
- EF Core SQL Server/Design et `dotnet-ef` 10.0.10 ;
- cible pilote Express non disponible sur ce poste ;
- confiance TLS locale toujours provisoire via `-C` / `TrustServerCertificate` limité aux processus de développement.

## Migration et schéma

Deux migrations sont appliquées :

1. `InitialCatalogPersistenceV01` ;
2. `CatalogPersistenceEnhancementsV01`.

La seconde ajoute uniquement `catalog.Products.IsActive bit NOT NULL DEFAULT 1` et `IX_catalog_Products_IsActive`. Les cinq tables Catalogue restent inchangées. Aucune migration automatique ou donnée de migration n’existe.

## Seed manuel

`database/dev/seed_synthetic_catalog_v01.sql` a été exécuté deux fois avec la codepage UTF-8 explicite. Résultat stable :

- 10 produits `DEMO-000001` à `DEMO-000010` ;
- 4 catégories Demo ;
- 10 barcodes non réels ;
- 10 historiques de prix initiaux ;
- 0 duplication ;
- aucune donnée clinique, personnelle, bancaire ou réelle.

Le premier essai a été annulé intégralement parce que les options ANSI requises par l’index filtré n’étaient pas explicites. Les scripts seed/clear fixent désormais ces options. Un passage clear ciblé a également confirmé que seules les références `DEMO-*` et leurs dépendances sont supprimées.

## Lectures SQL et POS

Les tests SQL réels couvrent :

- lecture existante/absente par `ProductId` ;
- recherche par casse différente, nom, présentation, `InternalCode` et barcode ;
- lecture exacte référence/barcode ;
- code inconnu sans création ;
- limite SQL ;
- une seule commande SQL pour la recherche POS, sans N+1 ;
- produit désactivé exclu ;
- prix SQL retourné à Sales.

## Écritures, transactions et concurrence

- création produit + paramètres + barcode + historique atomique ;
- catégorie absente : aucune donnée partielle ;
- référence et barcode dupliqués traduits ;
- prix négatif rejeté avant store ;
- prix courant et historique ajoutés dans le même commit ;
- rowversion obsolète rejetée sans nouvel historique ;
- activation/désactivation globale et locale ;
- barcode actif/inactif ;
- catégorie modifiée avec conflit détecté puis désactivée sans suppression physique.

## Architecture et sécurité

- Domain sans EF Core ;
- Application sans Infrastructure/EF/`IQueryable` ;
- Desktop sans `DbContext` ;
- entités EF internes ;
- aucun repository générique ;
- aucun `EnsureCreated`, `Database.Migrate` au démarrage ou logging sensible ;
- aucun fallback vers le catalogue mémoire ;
- aucune chaîne de connexion complète affichée ;
- aucune table Stock ou Sales créée.

Le scan secrets/PII n'a trouvé aucun secret réel ni donnée personnelle dans le périmètre Sprint 10. Le smoke Release a également maintenu le shell actif pendant quatre secondes, puis l'a arrêté proprement, avec et sans `PHARMACY_SQLSERVER_CONNECTION`.

## Commandes de validation

```powershell
dotnet tool restore
dotnet restore .\Pharmacy.slnx
dotnet build .\Pharmacy.slnx -c Debug --no-restore
dotnet test .\Pharmacy.slnx -c Debug --no-build
dotnet build .\Pharmacy.slnx -c Release --no-restore
dotnet test .\Pharmacy.slnx -c Release --no-build
```

Résultat observé dans les deux configurations : 300 tests réussis, 0 échec, 0 ignoré, build sans avertissement ni erreur.

Avec `PHARMACY_SQLSERVER_CONNECTION` fourni temporairement et ciblant `Pharmacy_Development` :

```powershell
dotnet ef migrations list `
  --project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --startup-project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --context PharmacyDbContext
dotnet ef migrations has-pending-model-changes `
  --project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --startup-project .\src\Pharmacy.Infrastructure\Pharmacy.Infrastructure.csproj `
  --context PharmacyDbContext
```

Seed et vérification :

```powershell
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\dev\seed_synthetic_catalog_v01.sql
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint10_catalog.sql
```

Lancement :

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale fournie hors Git vers Pharmacy_Development>'
dotnet run --project .\src\Pharmacy.Desktop\Pharmacy.Desktop.csproj -c Debug --no-build
Remove-Item Env:PHARMACY_SQLSERVER_CONNECTION
```

## Revue SSMS demandée

- [ ] dix références Demo et aucune autre donnée réelle ;
- [ ] quatre catégories ;
- [ ] dix paramètres locaux ;
- [ ] dix barcodes Demo uniques ;
- [ ] prix et historiques ;
- [ ] `Products.IsActive` ;
- [ ] rowversions produit/paramètres/barcodes/catégories ;
- [ ] deux migrations et aucune migration en attente ;
- [ ] seconde exécution du seed sans duplication.

## Revue Desktop demandée

- [ ] liste Catalogue, recherche et filtres ;
- [ ] détail, création et modification ;
- [ ] activation/désactivation ;
- [ ] barcode ;
- [ ] prix et historique ;
- [ ] catégorie ;
- [ ] conflit compréhensible et anti-double-soumission ;
- [ ] clavier, focus et responsive 1366 × 768 / 1920 × 1080 ;
- [ ] POS par référence et barcode Demo ;
- [ ] prix SQL et inactifs absents ;
- [ ] stock synthétique et vente non persistée ;
- [ ] panne SQL sûre ;
- [ ] Diagnostic sans secret et UI Lab accessible.

## Captures demandées

1. SSMS avec les dix produits Demo ;
2. liste Catalogue ;
3. détail produit ;
4. création ou modification ;
5. POS utilisant SQL ;
6. Diagnostic ou panne SQL ;
7. résultat du script de vérification.

## Limites

La revue visuelle humaine, SQL Server Express et la confiance TLS durable restent ouvertes. Le stock et toutes les ventes restent non persistés par conception Sprint 10.
