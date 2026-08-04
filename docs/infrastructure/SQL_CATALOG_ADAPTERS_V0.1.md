# Adaptateurs SQL Catalogue V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS`

## Lecteur SQL

`SqlProductCatalogReader` implémente `ICatalogProductReader` et le contrat POS `IProductCatalogReader` avec des contextes courts créés par `IDbContextFactory<PharmacyDbContext>`.

Les listes et recherches POS utilisent :

- `AsNoTracking` ;
- projection SQL directe ;
- filtres appliqués avant matérialisation ;
- limite 25 pour le POS et limite contrôlée pour la gestion ;
- une requête pour une recherche POS, sans N+1 ;
- aucun type EF renvoyé hors Infrastructure.

Les détails utilisent un `AsSplitQuery` borné sur un seul produit afin de charger les deux collections sans produit cartésien ni N+1 variable.

## Vendabilité POS

Un produit est projeté vers Sales seulement si :

- `Products.IsActive = 1` ;
- `ProductLocalSettings.IsActive = 1` ;
- `IsSellable = 1` ;
- `InternalCode` existe ;
- `CurrentSalePrice >= 0`.

La référence Sales vient exclusivement de `ProductLocalSettings.InternalCode`. Un barcode actif est également accepté pour la lecture exacte.

## Recherche et normalisation

La recherche couvre nom, `PackageDescription`, `InternalCode` et barcode actif. La normalisation barcode est déterministe : trim puis majuscules invariantes. Les zéros initiaux sont conservés.

## Store SQL

`SqlCatalogStore` implémente les stores produit et catégorie spécifiques. Il n’existe aucun `IRepository<T>`.

Chaque opération multi-table prépare toutes les modifications dans un contexte court et effectue un seul `SaveChangesAsync`. EF Core crée alors une transaction SQL atomique : produit, paramètres, barcodes et historique sont tous validés ou tous annulés.

Le changement de prix modifie `CurrentSalePrice` et ajoute l’historique dans le même commit. Le modèle Phase 3 étant append-only sans date de fin, aucun ancien historique n’est clôturé ou modifié.

## Contraintes et erreurs SQL

Les doublons sont d’abord détectés par requêtes exactes. En cas de course, les codes SQL Server 2601/2627 déclenchent une nouvelle détection par valeur pour distinguer référence et barcode. Le code 547 et les rowversions sont traduits selon l’opération. La traduction ne dépend donc pas uniquement d’un message SQL localisé.

`DbUpdateConcurrencyException` n’est ni écrasée, ni retentée. Les erreurs de connexion reconnues deviennent `catalog.database.unavailable` sans exposer la connexion.

## Migration Sprint 10

Le schéma Sprint 9 ne contenait pas `Products.IsActive`, pourtant requis pour l’activation globale et la vendabilité. La migration minimale `CatalogPersistenceEnhancementsV01` ajoute uniquement :

- `catalog.Products.IsActive bit NOT NULL DEFAULT 1` ;
- `IX_catalog_Products_IsActive`.

La migration Sprint 9 reste inchangée. Aucun seed n’est inclus dans les migrations.

## Composition et panne SQL

`AddSqlCatalogAdaptersFromEnvironment` lit la variable existante hors dépôt et refuse comme composition Desktop toute base autre que `Pharmacy_Development`. Si la variable manque ou est invalide, une factory indisponible est enregistrée : le shell démarre, Catalogue/POS retournent une erreur structurée et aucun fallback mémoire n’est effectué.

## Stock synthétique

`SyntheticCatalogStockAvailabilityReader` reçoit le `ProductId` déjà identifié par SQL et conserve une quantité déterministe dans un dictionnaire concurrent en mémoire. Aucun identifiant produit n’est codé en dur et aucune table Stock n’est créée.

## Diagnostic

`SqlCatalogDiagnosticsReader` affiche seulement le fournisseur, le nom de base, la connectivité, l’état des migrations et le nombre de produits Demo actifs. Serveur complet, utilisateur, mot de passe et chaîne de connexion ne sont jamais renvoyés au Desktop.
