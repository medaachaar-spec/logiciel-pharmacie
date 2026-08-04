# Inventory Physical Schema V0.1

Date : 2026-08-04

Statut : `IMPLEMENTED — SQL INVENTORY VALIDATION PASS — MANUAL STOCK REVIEW REQUIRED`

## 1. Périmètre

Cette version matérialise uniquement le stock réel autorisé au Sprint 11 : emplacements techniques, lots, balances, mouvements append-only et sessions d’inventaire. Elle ne crée aucune table Sales, Purchasing, Cash, Customer ou Security et ne persiste aucune vente.

## 2. Matrice de traçabilité préalable à la migration

| Élément Inventory | Source exacte | Classification |
|---|---|---|
| Schéma SQL `inventory` | Prompt Sprint 11, §8 | confirmé par le prompt |
| `StockMovement` historique et `StockBalance` état courant | ADR-005, p. 7 ; Phase 2, INV-001/002, p. 6 ; Phase 3, §3.3.2/3.3.3, p. 9-10 | règle fonctionnelle validée |
| Mouvement et balance dans la même transaction | ADR-005, p. 7 ; Phase 2, INV-002, p. 6 | règle fonctionnelle validée |
| Stock négatif interdit | Phase 2, INV-003, p. 6 ; prompt Sprint 11, §7/10 | règle fonctionnelle validée |
| Journal des mouvements append-only | Phase 3, §3.3.3, p. 9-10 | confirmé |
| Idempotence par `OperationId` | ADR-005, p. 7 ; Phase 3, §3.3.3 et §3.12.1, p. 10/24 | confirmé |
| `rowversion` sur agrégats mutables | ADR-005, p. 7 ; Phase 3, §3.1.8 et §3.3, p. 5/9-10 | confirmé |
| `StockLocations` et stock par emplacement | Prompt Sprint 11, §7/8/11 | décision technique exigée par le sprint ; besoin terrain encore à confirmer en Phase 2 p. 7 |
| `StockBatches` relié à `catalog.Products` | Phase 3, §3.3.1, p. 9 ; prompt Sprint 11, §8 | confirmé ; nom physique adapté au prompt |
| Lot facultatif selon `TracksLots` | Phase 2, CAT/INV, p. 4/6 ; Phase 3, §3.2.2/3.3.1, p. 6/9 | confirmé |
| Expiration `date` facultative selon `TracksExpiry` | Phase 3, §3.3.1, p. 9 ; prompt Sprint 11, §7/8 | confirmé |
| Lot expiré visible mais non vendable | Phase 2, INV-005, p. 6 ; prompt Sprint 11, §7/11 | règle fonctionnelle validée |
| FEFO par expiration la plus proche, proposition seulement | Phase 2, INV-004, p. 6 ; Phase 4, p. 8 ; prompt Sprint 11, §11 | règle fonctionnelle validée ; aucune affirmation réglementaire |
| Aucun seuil d'expiration inventé | Phase 2, INV-006, p. 6 ; SCOPE configurable ; prompt Sprint 11, §6/11 | confirmé |
| Sessions et lignes d'inventaire | Phase 2, INV-007, p. 6-7 ; Phase 3, §3.3.4/3.3.5, p. 10 | confirmé |
| Capture théorique conservée | Phase 2, workflow inventaire p. 7 ; prompt Sprint 11, §12 | confirmé par le prompt |
| Finalisation atomique en mouvements d'écart | Phase 2, INV-007, p. 6 ; Phase 3, §3.3.5, p. 10 | règle fonctionnelle validée |
| Six tables physiques minimales | Prompt Sprint 11, §8 | confirmé par le prompt |
| Quantités entières `int` | Prompt Sprint 11, §7/8 ; Phase 3, §3.1.5 autorise une précision générale différente | décision du sprint, vente fractionnée différée |
| `ReservedQuantity` omis | Phase 2, INV-009, p. 6 ; prompt Sprint 11, §11 | différé : aucune réservation au Sprint 11 |
| Fournisseur/réception d'origine du lot omis | Phase 3, §3.3.1, p. 9 ; hors périmètre Sprint 11 | différé au module Purchasing |
| Auteur, permission, motif configurable et audit complet | Phase 2/3 ; hors périmètre Sprint 11 | différé ; note/source technique minimale seulement |
| `IsActive` sur lot au lieu d'un workflow de blocage détaillé | Prompt Sprint 11, §8/11 | décision technique minimale ; processus périmés/endommagés encore à confirmer |
| États `Draft`, `InProgress`, `Completed`, `Cancelled` | Prompt Sprint 11, §7/8/12 | confirmé par le prompt ; `ReadyForReview`/`Validated` Phase 3 différés |
| Empreinte textuelle d'opération | Prompt Sprint 11, §10 | décision technique pour distinguer répétition et conflit |
| Données `DEMO-*` seulement | ADR-011, p. 13 ; prompt Sprint 11, §13 | donnée synthétique |

## 3. Tables implémentées

### `inventory.StockLocations`

Clé `bigint IDENTITY`, code unique, nom, indicateurs actif/principal, création UTC et `rowversion`. L'emplacement principal est une commodité technique de prototype, pas une règle terrain confirmée.

### `inventory.StockBatches`

Clé `bigint IDENTITY`, FK produit, numéro de lot facultatif, expiration `date` facultative, état actif, création UTC et `rowversion`. La paire produit/numéro de lot est unique lorsque le numéro existe.

### `inventory.StockBalances`

Clé `bigint IDENTITY`, FK emplacement/produit/lot facultatif, quantité disponible `int`, mise à jour UTC et `rowversion`. Deux index filtrés garantissent une balance unique avec ou sans lot. Aucun total produit redondant n'est stocké.

### `inventory.StockMovements`

Journal immutable avec `OperationId uniqueidentifier` unique, empreinte, produit, emplacement, lot, type, quantité positive, valeurs avant/après, motif, source et instants UTC. Les contraintes vérifient le sens du mouvement et interdisent les balances négatives.

### `inventory.InventorySessions`

Clé `bigint IDENTITY`, référence unique, emplacement, statut, dates UTC, note, identifiants d'opération de création/finalisation/annulation et `rowversion`.

### `inventory.InventoryCountLines`

Clé `bigint IDENTITY`, session, produit, lot facultatif, quantité théorique capturée, quantité comptée nullable, dernière opération de comptage et `rowversion`. Deux index filtrés garantissent une ligne unique avec ou sans lot.

## 4. Relations et suppressions

Toutes les FK utilisent `NO ACTION`. Aucun historique, lot, mouvement, solde ou inventaire n'est supprimé en cascade.

## 5. Décisions de migration

- création du schéma et des six tables dans la migration `20260804181233_InventoryPersistenceV01` ;
- migrations Sprint 9/10 inchangées ;
- aucun seed dans la migration ;
- script idempotent généré par EF Core ;
- `Up`, `Down`, types, contraintes, index et FK revus avant application ;
- script EF idempotent généré sous `database/scripts/003_inventory_persistence_v01_idempotent.sql` ;
- migration appliquée à `Pharmacy_Development`, sans migration en attente.

## 6. Validation physique

- six tables `inventory` et aucune table hors périmètre créée par la migration ;
- neuf `rowversion` sur les tables mutables Catalogue/Inventory ;
- quantités `int`, dates de péremption `date`, instants `datetime2(7)` ;
- aucune FK en cascade ;
- contraintes SQL sur quantité positive, solde non négatif et cohérence avant/après ;
- scénario SQL réel sur base temporaire : migration, écritures, concurrence, idempotence, FEFO, inventaire, contrainte négative et suppression de la base en `finally` ;
- rollback complet et réapplication couverts par le test de persistance existant sur l’ensemble des migrations.

## 7. Limites

Les emplacements multiples, motifs administrables, permissions, auteur utilisateur, réservation, achats, ventes, destruction et réglementation des lots restent hors de cette version. Le panier ne réserve ni ne consomme le stock.
