# Catalogue Application V0.1

Date : 2026-08-03

Statut : `IMPLEMENTED — SQL CATALOG VALIDATION PASS — MANUAL CATALOG REVIEW REQUIRED`

## Objectif

Fournir les cas d’utilisation Catalogue indépendants de SQL Server et du Desktop. Application expose uniquement des commandes, requêtes, contrats, modèles de lecture immuables et erreurs structurées.

## Architecture

```text
Desktop
  -> handlers Catalogue Application
    -> ICatalogProductReader / ICatalogProductStore
    -> ICatalogCategoryReader / ICatalogCategoryStore
    -> ICatalogDiagnosticsReader
      -> Infrastructure SQL
```

Application ne référence ni Infrastructure, EF Core, `DbContext`, `IQueryable` ou chaîne de connexion.

## Modèles de lecture

- `CatalogProductListItem` : référence, identité, présentation, catégorie, prix, états et barcode principal ;
- `CatalogProductDetails` : identité, paramètres locaux, deux rowversions Base64, barcodes et historique des prix ;
- `CatalogCategoryView` : catégorie et rowversion Base64 ;
- `CatalogBarcodeView` et `CatalogPriceHistoryView` : collections copiées et exposées en lecture seule ;
- `CatalogDiagnosticsView` : fournisseur, base, connectivité, migration et nombre Demo sans connexion complète.

## Requêtes

- recherche Catalogue avec texte, filtre actif/inactif, catégorie et limite maximale 100 ;
- détail exact par `ProductId` ;
- liste ordonnée des catégories ;
- diagnostic sûr de la persistance.

La recherche POS existante continue d’utiliser `IProductCatalogReader`, désormais implémenté par le lecteur SQL.

## Commandes produit

- création transactionnelle de `Product`, `ProductLocalSettings`, barcodes éventuels et premier historique de prix éventuel ;
- modification de l’identité et des paramètres locaux, hors prix ;
- activation/désactivation globale et locale ;
- ajout et mise à jour d’un barcode ;
- changement de prix dédié avec historique append-only.

La création ne crée aucun stock. Le prix n’est jamais modifié par la commande d’identité.

## Commandes catégorie

- création ;
- modification ;
- activation/désactivation.

Aucun contrat de suppression physique n’est exposé.

## Validation

Application vérifie les identifiants, textes obligatoires, longueurs, prix non négatifs, quantité scale 0 à 3, cohérence des seuils et rowversions Base64 de huit octets. Infrastructure revérifie les contraintes qui dépendent de l’état SQL.

## Concurrence

Les commandes de modification transportent les rowversions reçues à la lecture. Un conflit produit une erreur structurée et n’est jamais rejoué automatiquement :

- `catalog.product.concurrency_conflict` ;
- `catalog.category.concurrency_conflict`.

## Erreurs stables

- `catalog.product.not_found` ;
- `catalog.category.not_found` ;
- `catalog.reference.duplicate` ;
- `catalog.barcode.duplicate` ;
- `catalog.barcode.not_found` ;
- `catalog.price.invalid` ;
- `catalog.product.concurrency_conflict` ;
- `catalog.category.concurrency_conflict` ;
- `catalog.validation.failed` ;
- `catalog.database.unavailable`.

## Limites

Le stock reste synthétique et en mémoire. Brouillons Sales, suspensions, paiements, impression et finalisation restent non persistés. Aucun lot, mouvement de stock, vente, API HTTP, donnée clinique ou règle fiscale n’est ajouté.
