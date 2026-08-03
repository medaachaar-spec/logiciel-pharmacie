# Catalog Physical Schema V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL SERVER DEVELOPER VALIDATION PASS — EXPRESS ENVIRONMENT GATE PENDING`

## 1. Périmètre

Cette fondation matérialise uniquement le Catalogue confirmé. Elle ne contient aucune donnée, table de vente, stock, lot, paiement, caisse, fournisseur, client, identité, audit ou reporting.

La Phase 3 nomme le concept central `ProductLocalSettings` et relie directement `ProductBarcode` et `ProductPriceHistory` à `Product`. Conformément à la clause de priorité du prompt Sprint 9, le schéma physique utilise donc `catalog.ProductLocalSettings` à la place de la cible indicative `ProductReferences`, et conserve les relations Phase 3.

## 2. Matrice de traçabilité préalable à la migration

| Élément physique | Source exacte | Classification |
|---|---|---|
| Schéma `catalog` | Prompt Sprint 9, §8 « Schéma SQL » | confirmé par le prompt |
| Clés `bigint IDENTITY` | Phase 3, §3.1.1, page 4 ; prompt Sprint 9, §8 « Types » | confirmé |
| `datetime2` UTC | Phase 3, §3.1.3, page 4 | confirmé |
| Prix `decimal(19,4)` | Phase 3, §3.1.4, page 4 | confirmé |
| Quantités/seuils `decimal(18,3)` | Phase 3, §3.1.5, page 4 | confirmé |
| Suppression `NO ACTION` | Phase 3, §3.1.9, page 5 ; ADR-005, page 7 | confirmé |
| `rowversion` sur les données mutables | Phase 3, §3.1.8, page 5 et §3.2.2, page 7 | confirmé |
| `catalog.ProductCategories` | Phase 3, §3.2.4, page 7 ; prompt Sprint 9, §8 | confirmé |
| `catalog.Products` | Phase 3, §3.2.1, page 6 ; prompt Sprint 9, §8 | confirmé |
| `catalog.ProductLocalSettings` en 1–1 avec `Products` | Phase 3, §3.2.2, pages 6–7 | confirmé ; remplace le nom indicatif `ProductReferences` |
| Catégorie principale obligatoire par paramètres locaux | Phase 2, CAT-005, page 4 ; Phase 3, §3.2.2, page 6 | confirmé |
| `catalog.ProductBarcodes` en N–1 vers `Products` | Phase 2, CAT-002/CAT-003, page 4 ; Phase 3, §3.2.3, page 7 | confirmé |
| Code-barres textuel normalisé unique | Phase 2, CAT-002/CAT-003, page 4 ; Phase 3, §3.12.1, page 24 | confirmé |
| Un seul code principal actif par produit | Phase 3, §3.2.3, page 7 | confirmé |
| `catalog.ProductPriceHistories` en N–1 vers `Products` | Phase 2, CAT-007, page 4 ; Phase 3, §3.2.7, pages 7–8 | confirmé |
| Historique de prix append-only sans période de fin inventée | Phase 3, §3.2.7, pages 7–8 | confirmé / absence de période confirmée |
| Longueurs `nvarchar` explicites | Prompt Sprint 9, §8 « Types » | décision technique documentée ci-dessous |
| Noms explicites de PK, FK, contraintes et index | Prompt Sprint 9, §8 « Contraintes et index » | confirmé par le prompt |
| Aucun seed produit | Phase 2, CAT-004/CAT-009, page 4 ; prompt Sprint 9, §8 « Données » | confirmé |
| `Manufacturer`, `UnitOfMeasure`, auteur/import de prix | Phase 3, §3.2, pages 6–8 ; hors cible minimale Sprint 9 | différé |
| PPV/PPH | Phase 2, page 20 ; Phase 3, §3.2.7, page 8 | différé — à confirmer |

## 3. Dictionnaire physique

### `catalog.ProductCategories`

| Colonne | Type | Null | Rôle |
|---|---|---:|---|
| `Id` | `bigint IDENTITY` | non | clé technique |
| `Code` | `nvarchar(64)` | non | code configurable unique |
| `Name` | `nvarchar(160)` | non | libellé de catégorie |
| `IsActive` | `bit` | non | désactivation fonctionnelle |
| `DefaultTracksLots` | `bit` | non | valeur proposée pour le suivi des lots |
| `DefaultTracksExpiry` | `bit` | non | valeur proposée pour la péremption |
| `DefaultDiscountAllowed` | `bit` | non | valeur proposée pour les remises |
| `SortOrder` | `int` | non | ordre d’affichage non négatif |
| `RowVersion` | `rowversion` | non | concurrence optimiste |

### `catalog.Products`

| Colonne | Type | Null | Rôle |
|---|---|---:|---|
| `Id` | `bigint IDENTITY` | non | identité stable de la référence commerciale |
| `Name` | `nvarchar(256)` | non | désignation commerciale |
| `GenericName` | `nvarchar(256)` | oui | libellé générique facultatif |
| `Dosage` | `nvarchar(128)` | oui | information facultative non interprétée |
| `Form` | `nvarchar(128)` | oui | forme facultative non interprétée |
| `PackageDescription` | `nvarchar(256)` | oui | conditionnement |
| `BrandName` | `nvarchar(160)` | oui | marque facultative |
| `CreatedAtUtc` | `datetime2(7)` | non | création technique UTC |
| `UpdatedAtUtc` | `datetime2(7)` | non | dernière modification UTC |
| `RowVersion` | `rowversion` | non | concurrence optimiste |

`ManufacturerId` et `UnitOfMeasureId` sont différés avec leurs tables confirmées mais hors cible Sprint 9 ; aucune FK orpheline n’est créée.

### `catalog.ProductLocalSettings`

| Colonne | Type | Null | Rôle |
|---|---|---:|---|
| `ProductId` | `bigint` | non | PK et FK 1–1 vers `Products` |
| `InternalCode` | `nvarchar(128)` | oui | référence interne unique si renseignée |
| `CategoryId` | `bigint` | non | catégorie principale |
| indicateurs `Is*` / `Tracks*` | `bit` | non | activation, vente, achat, lots, péremption, remise |
| `QuantityScale` | `tinyint` | non | précision 0 à 3 |
| `StorageLocation` | `nvarchar(128)` | oui | emplacement facultatif |
| `MinimumStock` / `MaximumStock` | `decimal(18,3)` | oui | seuils cohérents et non négatifs |
| `CurrentSalePrice` | `decimal(19,4)` | non | prix courant non négatif |
| `CurrencyCode` | `nvarchar(3)` | non | code devise de trois caractères |
| `RowVersion` | `rowversion` | non | concurrence optimiste |

### `catalog.ProductBarcodes`

`Id bigint IDENTITY`, `ProductId bigint`, valeurs originale et normalisée en `nvarchar(128)`, indicateurs principal/actif, type facultatif `nvarchar(32)`, date UTC et `rowversion`. La valeur normalisée est unique dans l’installation ; un index filtré garantit un seul code principal actif par produit.

### `catalog.ProductPriceHistories`

`Id bigint IDENTITY`, `ProductId bigint`, ancien prix nullable et nouveau prix en `decimal(19,4)`, devise `nvarchar(3)`, origine `nvarchar(32)`, motif `nvarchar(256)` facultatif et date d’effet UTC `datetime2(7)`. Cette table est append-only dans ce périmètre et ne reçoit pas de `rowversion`.

## 4. Relations et suppressions

```text
ProductCategories 1 ─── N ProductLocalSettings N ─── 1 Products
Products          1 ─── N ProductBarcodes
Products          1 ─── N ProductPriceHistories
```

Toutes les FK utilisent `ON DELETE NO ACTION`. Un produit ou une catégorie référencée doit être désactivé plutôt que supprimé arbitrairement.

## 5. Décisions techniques locales

- Les longueurs sont des bornes de sûreté physiques, non des exigences légales ou pharmacien.
- Les colonnes PPV/PPH ne sont pas créées.
- Aucun champ clinique, fiscal non confirmé ou bancaire n’est ajouté.
- Aucun intervalle de validité des prix n’est inventé : seule `EffectiveAtUtc`, confirmée par Phase 3, est stockée.
- Les auteurs et imports de prix seront reliés lorsque leurs schémas autorisés existeront ; aucune FK spéculative n’est créée.

## 6. Données

La migration doit produire un schéma vide. Tous les scénarios automatisés emploient des valeurs synthétiques et des bases temporaires protégées par le préfixe `Pharmacy_Sprint09_Test_`.
