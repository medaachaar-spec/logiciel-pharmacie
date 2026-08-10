# Reporting SQL V0.1

## Périmètre

La migration `ReportingViewsAndAlertSettingsV01` crée le schéma `reporting`, une ligne de configuration et onze vues en lecture seule. Les vues restent granulaires : aucune table d’agrégats, aucun cache, aucun job et aucune donnée de démonstration ne sont ajoutés.

## Objets

| Objet | Grain | Usage |
|---|---|---|
| `reporting.ReportSettings` | singleton `Id = 1` | activation des alertes et horizon explicite |
| `vw_SalesSummary` | une vente | brut, payé, crédit, remboursements préagrégés, net documenté |
| `vw_ProductSales` | une ligne de vente | quantités/montants vendus et retournés |
| `vw_Payments` | un paiement | moyen, statut, montant et date de confirmation |
| `vw_StockSummary` | un produit actif et vendable | stock vendable actuel et seuil local |
| `vw_ExpiryAlerts` | un lot actif avec quantité | expiration et jours restants |
| `vw_PurchaseOrders` | une commande | quantités commandées/reçues et montant historique disponible |
| `vw_Receipts` | une réception | quantité et coût historique reçu |
| `vw_InventoryAdjustments` | un mouvement manuel/inventaire | avant, après, quantité et justification |
| `vw_CustomerCredit` | un client | crédit généré, payé et solde du journal immuable |
| `vw_ReturnsSummary` | un retour | remboursement, quantité retournée et remise en stock |
| `vw_CashMovements` | une écriture de caisse | entrée/sortie et source |

## Prévention des doublons

Les retours sont d’abord regroupés par vente ou ligne, puis joints aux ventes. Les paiements et allocations de lots ne sont jamais joints à la même agrégation. Les montants d’achat utilisent les prix historiques des lignes de commande/réception et jamais le prix catalogue courant.

## Stock et péremption

Le stock vendable inclut uniquement les quantités positives des produits/local settings actifs et vendables. Un lot doit être actif et non périmé à la date UTC courante ; un solde sans lot reste admissible. Le stock bas compare ce total à `catalog.ProductLocalSettings.MinimumStock` uniquement lorsque ce seuil existe et que l’alerte est activée.

## Contraintes et concurrence

- `ReportSettings.Id = 1` ;
- `ExpiryHorizonDays` nullable et, lorsqu’il existe, supérieur ou égal à zéro ;
- `rowversion` pour la concurrence optimiste ;
- aucun seuil ou horizon seedé ; les deux familles d’alertes sont désactivées initialement.

Les index opérationnels existants sur dates, produits, statuts et journaux sont réutilisés. Aucun index de vue ou pré-calcul n’est ajouté sans mesure démontrant sa nécessité.

## Exploitation

La migration n’est jamais appliquée au démarrage Desktop. La connexion est fournie hors dépôt. Le contrôle en lecture seule se trouve dans `database/verification/verify_sprint16_reporting.sql`. Les tests utilisent une base temporaire `Pharmacy_Sprint16_Test_*`, migrée puis supprimée en `finally`.
