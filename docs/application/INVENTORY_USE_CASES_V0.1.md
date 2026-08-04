# Inventory Use Cases V0.1

Date : 2026-08-04  
Statut : `IMPLEMENTED — SQL ADAPTER VALIDATION PASS`

## Contrats

- `IInventoryDateProvider` fournit la date métier testable ;
- `IInventoryReader` expose emplacements, résumé, détail, mouvements, expirations, FEFO, sessions et diagnostic ;
- `IInventoryStore` expose les mutations sans dépendre d’EF Core.

Les modèles Application sont immuables ou exposent des collections en lecture seule. Les tokens de concurrence sont des `rowversion` encodées en Base64. Chaque mutation reçoit un `OperationId` non vide.

## Requêtes

- lister les emplacements ;
- rechercher le stock par texte, emplacement et disponibilité ;
- lire le détail produit, les balances et les lots expirés séparément ;
- lire le journal des mouvements ;
- lire les lots expirant dans un horizon fourni ou déjà expirés ;
- proposer une allocation FEFO sans consommer le stock ;
- lister/lire les sessions d’inventaire ;
- obtenir un diagnostic Inventory sans secret.

## Commandes

- créer/activer un emplacement ;
- créer un lot selon `TracksLots`/`TracksExpiry` ;
- enregistrer une ouverture, hausse ou baisse manuelle ;
- créer puis démarrer une session ;
- enregistrer chaque comptage ;
- finaliser atomiquement ou annuler.

## Garanties

Les handlers valident identifiants, textes, quantités, types, `OperationId` et tokens avant le store. Une baisse insuffisante, une version obsolète, une réutilisation incohérente d’`OperationId` ou un état de session invalide retourne un échec structuré sans retry aveugle. La finalisation exige toutes les lignes comptées et transforme chaque écart non nul en mouvement d’inventaire dans la transaction de finalisation.

## Disponibilité Sales

`IStockAvailabilityReader` reste un contrat Sales existant. Son adaptateur Sprint 11 additionne uniquement les balances de produits/emplacements/lots actifs et non expirés. La vente simulée lit cette disponibilité mais ne réserve et ne décrémente rien.
