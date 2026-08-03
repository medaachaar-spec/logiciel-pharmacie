# Persistence Foundation V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING`

## 1. Objectif

Fournir la première infrastructure EF Core/SQL Server du projet sans connecter le POS ni les cas d’utilisation Sales à une base. La fondation contient seulement le schéma physique Catalogue V0.1 et les outils de migration contrôlée.

## 2. Environnement validé

- SQL Server : instance Windows locale par défaut ;
- version : `17.0.1125.2` ;
- édition : SQL Server 2025 Developer 64 bits ;
- niveau de compatibilité de `Pharmacy_Development` : 170 ;
- authentification de validation : sécurité intégrée Windows ;
- cible pilote : SQL Server 2025 Express, non disponible sur ce poste et donc encore à valider.

Le client ODBC 18 refuse la connexion chiffrée standard parce que la chaîne du certificat local n’est pas approuvée. `TrustServerCertificate=True` a été utilisé uniquement dans les commandes locales temporaires de ce sprint. Ce contournement n’est ni versionné, ni une décision de déploiement.

## 3. Packages et outil

- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.10 ;
- `Microsoft.EntityFrameworkCore.Design` 10.0.10, `PrivateAssets=all` ;
- outil local `dotnet-ef` 10.0.10 dans `.config/dotnet-tools.json`.

Domain et Application ne référencent aucun package EF Core. Aucun provider InMemory, SQLite, Dapper, outil de migration concurrent ou package preview n’est ajouté.

## 4. Architecture

```text
Pharmacy.Infrastructure
└── Persistence
    ├── PharmacyDbContext
    ├── PharmacyDbContextFactory
    ├── PharmacySqlServerOptions
    ├── DependencyInjection
    ├── Entities/Catalog
    ├── Configurations/Catalog
    └── Migrations
```

Les entités et configurations sont internes à Infrastructure. Les tests d’intégration disposent uniquement d’un accès ami ciblé pour vérifier le comportement SQL réel. Desktop n’appelle ni `DbContext`, ni l’extension de persistance et continue d’enregistrer `AddInMemorySalesAdapters`.

## 5. DbContext

`PharmacyDbContext` expose en interne cinq `DbSet` correspondant au périmètre Catalogue. Les configurations Fluent API sont séparées et chargées depuis l’assembly Infrastructure. Aucune configuration géante n’est placée dans `OnModelCreating`.

Les options communes appliquent :

- `UseSqlServer` ;
- compatibilité 170 ;
- `EnableRetryOnFailure()` ;
- assembly de migrations explicite ;
- aucun logging sensible.

## 6. Factory design-time et variables

`PharmacyDbContextFactory` lit uniquement :

`PHARMACY_SQLSERVER_CONNECTION`

Une valeur absente ou vide provoque une erreur qui cite seulement le nom de la variable. La valeur n’est ni recopiée dans le message, ni journalisée.

Les tests SQL réels utilisent :

`PHARMACY_TEST_SQLSERVER_CONNECTION`

Les commandes doivent définir ces variables uniquement dans le processus courant puis les supprimer. Aucune chaîne réelle, aucun compte SQL et aucun mot de passe n’est conservé dans le dépôt.

## 7. Migration contrôlée

La migration unique est `InitialCatalogPersistenceV01`. Le projet Infrastructure est utilisé comme startup design-time afin de garder le package Design hors Desktop.

Artefacts :

- migration, designer et snapshot sous `Persistence/Migrations` ;
- script idempotent `database/scripts/001_initial_catalog_foundation_idempotent.sql` ;
- procédure de commande dans `database/migrations/README.md` ;
- script de vérification en lecture seule sous `database/verification`.

`Up` crée le schéma et cinq tables vides. `Down` supprime les cinq tables dans l’ordre compatible avec les FK. Aucun seed n’est généré.

## 8. Base de développement

`Pharmacy_Development` a été créée uniquement après confirmation de son absence. Elle contient une ligne dans `dbo.__EFMigrationsHistory`, cinq tables Catalogue et zéro donnée Catalogue. `dotnet ef migrations has-pending-model-changes` ne détecte aucun écart et `database update` confirme que la base est à jour.

## 9. Tests SQL réels

Le test d’intégration exige la variable de test et crée un nom aléatoire strictement conforme à :

`Pharmacy_Sprint09_Test_<12 caractères hexadécimaux>`

Il refuse toute opération destructive sur un autre nom. Dans un `finally`, il ferme les pools, place uniquement cette base en mode mono-utilisateur puis la supprime. Les contrôles couvrent :

- migration `Up` ;
- tables, FK `NO ACTION` et `rowversion` ;
- graph synthétique Catalogue ;
- unicité du barcode et du barcode principal actif ;
- prix négatif refusé ;
- FK invalide refusée ;
- suppression du produit référencé refusée ;
- changement de `rowversion` ;
- conflit EF sur ancienne version et état final cohérent ;
- double exécution du script idempotent ;
- rollback vers `0` ;
- réapplication et comparaison du schéma ;
- migration en attente absente ;
- base temporaire supprimée.

## 10. Sécurité

- aucun `EnsureCreated` ou `EnsureDeleted` dans le code de production ;
- aucun `Database.Migrate` au démarrage ;
- aucun `EnableSensitiveDataLogging` ;
- aucun secret ou connexion réelle dans les sources, scripts ou rapports ;
- aucun login SQL ou utilisateur de production créé ;
- aucune donnée médicale, personnelle ou bancaire ;
- données de test exclusivement synthétiques.

## 11. Limites

- validation réalisée sur Developer, pas sur Express ;
- chaîne de confiance TLS locale à corriger ;
- aucune sauvegarde/restauration pilote dans ce sprint ;
- aucune persistance Sales, stock, lot, paiement ou autre module ;
- aucun seed Catalogue ;
- aucune connexion du POS à `PharmacyDbContext` ;
- `Manufacturer`, `UnitOfMeasure`, auteur/import de prix, PPV et PPH différés.

## 12. Suite sûre

Effectuer la revue SSMS, corriger la confiance TLS et répéter les mêmes preuves sur SQL Server 2025 Express avant de considérer le gate pilote. Toute future migration doit conserver revue `Up`/`Down`, script idempotent, base temporaire préfixée, rollback et sauvegarde vérifiée.
