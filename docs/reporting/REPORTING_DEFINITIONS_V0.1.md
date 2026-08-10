# Définitions Reporting V0.1

Toutes les bornes temporelles sont UTC, sous la forme `[début inclus, fin exclue)`. La devise affichée est MAD, seule devise opérationnelle stockée/configurée dans ce pilote. La date de génération et les filtres actifs sont toujours visibles.

| Indicateur | Définition et formule | Source | Grain | Exclusions / période |
|---|---|---|---|---|
| Ventes brutes | somme `Sales.GrandTotal` | `sales.Sales` | vente | ventes dont `CompletedAtUtc` est dans la période |
| Nombre de ventes | nombre de ventes persistées | `sales.Sales` | vente | même période |
| Remboursements | somme des retours `Completed` | `sales.SaleReturns` | retour | date du retour dans la période |
| Ventes nettes documentées | ventes brutes de la période moins remboursements de la période | ventes + retours | période | ce n’est ni une marge ni un résultat comptable |
| Paiements par moyen | somme et nombre des paiements `Completed` groupés par moyen | `sales.Payments` | moyen de paiement | date de confirmation dans la période |
| Crédit ouvert | somme des soldes clients positifs | `customers.CustomerLedgerEntries` | client | état actuel, hors filtre temporel |
| Règlements crédit | somme `CreditPayment` | journal client | écriture | date de l’écriture dans la période |
| Stock vendable | somme des quantités admissibles | balances/lots/produits | produit | état actuel ; lots inactifs ou périmés exclus |
| Ruptures | produit vendable dont stock actuel ≤ 0 | `vw_StockSummary` | produit | état actuel |
| Sous seuil | stock actuel strictement inférieur au `MinimumStock` non nul | local settings + stock | produit | uniquement si alerte activée |
| Lots périmés | lot actif avec quantité et `DaysRemaining < 0` | `vw_ExpiryAlerts` | lot | état actuel |
| Lots dans horizon | `0 ≤ DaysRemaining ≤ horizon` | lots + paramètre | lot | uniquement si alerte et horizon explicite activés |
| Commandes d’achat | commandes créées | `purchasing.PurchaseOrders` | commande | `CreatedAtUtc` dans la période |
| Quantité réceptionnée | somme des quantités des lignes reçues | réceptions/lignes | ligne | `ReceivedAtUtc` dans la période |
| Coût historique réception | quantité × prix d’achat de la ligne de réception | lignes réception | réception | jamais le prix courant du produit |
| Ajustements/inventaires | mouvements manuels et d’inventaire avec avant/après | mouvements stock | mouvement | date du mouvement dans la période |
| Retours/remboursements | montant, quantité, destination stock | retours/lignes | retour | date de retour dans la période |
| Cash | montant signé par type et source | journal caisse | écriture | date de l’écriture dans la période |

## Limites explicites

- `NetSales` signifie seulement `gross sales − refunds` sur la même période sélectionnée.
- Aucune marge, rentabilité, tendance, score, prévision, recommandation ou seuil implicite n’est produit.
- Les pages détaillées sont plafonnées à 200 lignes par défaut (maximum technique 1000) ; les indicateurs de synthèse sont calculés sur l’ensemble SQL filtré, pas sur cette page.
- Le crédit et le stock sont des états courants ; ils sont étiquetés comme tels pour ne pas être confondus avec une photographie historique.
