# Purchasing Physical Schema V0.1

## Statut

Implémenté par le Sprint 12. Schéma additif `purchasing`, sans donnée de démonstration dans la migration.

## Matrice de traçabilité

| Besoin confirmé | Source | Objet physique | Décision V0.1 |
|---|---|---|---|
| Fournisseur actif et historisable | Phase 2 PUR-001, Phase 3 | `purchasing.Suppliers` | `bigint IDENTITY`, code unique, `rowversion`, désactivation logique |
| Commande d’achat et statut contrôlé | Phase 2 PUR-005/006, Phase 3 | `purchasing.PurchaseOrders` | `Draft`, `Confirmed`, `PartiallyReceived`, `FullyReceived`, `Cancelled` |
| Snapshots Catalogue sur commande | Phase 3 | `purchasing.PurchaseOrderLines` | produit FK, code/nom snapshots, quantités `int`, prix `decimal(19,4)`, `rowversion` |
| Réception liée ou directe | Phase 2 PUR-007/008 | `purchasing.GoodsReceipts` | commande nullable, `OperationId` unique, BL unique par fournisseur, statut final `Completed` |
| Traçabilité stock par ligne | Phase 3 DATA-006 | `purchasing.GoodsReceiptLines` | FK produit, ligne commande, lot et mouvement ; prix d’achat snapshot |
| Idempotence de finalisation | Prompt Sprint 12 | `GoodsReceipts.OperationId`, `OperationFingerprint` | même opération/empreinte rejouée ; contenu différent refusé |
| Concurrence | ADR-007, Phase 3 | colonnes `rowversion` | fournisseur, commande, ligne de commande, réception |
| Intégrité transactionnelle | Phase 2/3 | adaptateur SQL | réception, lots, mouvements, balances et commande dans une transaction `Serializable` |

## Tables et contraintes

- `Suppliers` : code `nvarchar(32)` unique, nom `nvarchar(200)`, contacts facultatifs, actif, instants UTC et `rowversion`.
- `PurchaseOrders` : numéro unique, fournisseur, statut, dates métier `date`, instants UTC, motif d’annulation et `rowversion`.
- `PurchaseOrderLines` : une ligne fusionnée par commande/produit, `OrderedQuantity > 0`, `0 <= ReceivedQuantity <= OrderedQuantity`, prix facultatif non négatif, snapshots et `rowversion`.
- `GoodsReceipts` : numéro interne, `OperationId`, empreinte, fournisseur, commande facultative, emplacement, type, statut, BL, dates et `rowversion`.
- `GoodsReceiptLines` : quantité positive, prix non négatif, snapshots, lot facultatif selon configuration et mouvement Stock unique.

Toutes les clés primaires sont `bigint IDENTITY`. Les instants sont `datetime2(7)` UTC, les dates métier `date`, et les prix d’achat `decimal(19,4)` conformément à l’exception validée de Phase 3. Aucune cascade n’est autorisée ; toutes les relations utilisent `NO ACTION`.

## Intégration Inventory

Le type append-only `PurchaseReceipt` est ajouté aux mouvements. La contrainte de direction garantit `QuantityAfter = QuantityBefore + Quantity`. Une réception sans lot utilise la balance sans lot existante ; une réception suivie crée ou réutilise un lot compatible du même produit.

## Exclusions

Référencement fournisseur-produit, documents comptables, retours/avoirs, fiscalité, remises, paiements fournisseurs et marge sont reportés : aucune table ni colonne spéculative n’est créée.
