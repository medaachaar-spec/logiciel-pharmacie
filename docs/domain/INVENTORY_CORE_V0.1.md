# Inventory Core V0.1

Date : 2026-08-04  
Statut : `IMPLEMENTED — AUTOMATED VALIDATION PASS`

## Objet

Le noyau Inventory porte les règles de quantité, de lot, de péremption, de sens des mouvements et d’état des inventaires. Il reste indépendant d’EF Core, SQL Server, WPF et Blazor.

## Types

- identifiants typés `StockLocationId`, `StockBatchId`, `InventorySessionId` ;
- `StockQuantity`, entier positif ou nul ;
- `StockMovementQuantity`, entier strictement positif ;
- `LotNumber`, valeur non blanche et bornée ;
- `ExpiryDate`, comparaison déterministe à une `DateOnly` fournie ;
- `StockBalanceState`, transition immutable et refus du négatif ;
- `StockMovementType` : `Opening`, `ManualIncrease`, `ManualDecrease`, `InventoryIncrease`, `InventoryDecrease` ;
- `InventorySessionStatus` : `Draft`, `InProgress`, `Completed`, `Cancelled`.

## Invariants

Une balance ne descend jamais sous zéro. Une ouverture ou hausse additionne une quantité positive ; une baisse insuffisante échoue sans produire d’état partiel. `InventoryTrackingPolicy` exige un lot et/ou une expiration lorsque les paramètres Catalogue `TracksLots` et `TracksExpiry` le demandent. Une date égale à la date métier courante n’est pas expirée ; une date antérieure l’est.

## Erreurs stables

Seize codes `inventory.*` couvrent les identifiants absents, états inactifs, lots/péremptions requis, quantités, stock insuffisant, conflit d’opération, concurrence, états d’inventaire et indisponibilité SQL. Les messages sont présentables, mais le code stable reste la base de décision.

## Limites

La réservation, la vente fractionnée, les motifs administrables, l’identité de l’auteur, les destructions, achats/réceptions et obligations réglementaires détaillées sont différés. FEFO est une proposition fonctionnelle configurable, pas une affirmation réglementaire.
