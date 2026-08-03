# Migrations de base de données

Les migrations EF Core du projet sont versionnées dans :

`src/Pharmacy.Infrastructure/Persistence/Migrations`

## Règles

- Exécuter les commandes depuis la racine du dépôt.
- Restaurer l’outil local avec `dotnet tool restore`.
- Fournir la connexion uniquement par `PHARMACY_SQLSERVER_CONNECTION`.
- Ne jamais écrire une chaîne réelle, un compte SQL ou un mot de passe dans le dépôt.
- Ne jamais appeler `EnsureCreated`, `EnsureDeleted` ou `Database.Migrate` au démarrage Desktop.
- Examiner `Up`, `Down`, le snapshot et le script idempotent avant application.
- Sauvegarder et tester la restauration avant toute future migration pilote.

## Migration initiale

`InitialCatalogPersistenceV01` crée uniquement le schéma Catalogue V0.1. Elle ne contient aucun seed ni table Sales.

Le script idempotent associé est :

`database/scripts/001_initial_catalog_foundation_idempotent.sql`

## Commandes design-time

```powershell
$env:PHARMACY_SQLSERVER_CONNECTION = '<connexion locale fournie hors Git>'
dotnet tool restore
dotnet ef migrations list `
  --project src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj `
  --startup-project src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj `
  --context PharmacyDbContext
Remove-Item Env:PHARMACY_SQLSERVER_CONNECTION
```

Le projet Infrastructure sert de startup design-time afin de ne pas ajouter `Microsoft.EntityFrameworkCore.Design` au Desktop.
