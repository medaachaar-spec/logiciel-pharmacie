# Finalizer SQL Sales V0.1

Date : 2026-08-10

Statut : `IMPLEMENTED — SQL SERVER REAL TESTS PASS`

## Responsabilité

`SqlSaleFinalizer` implémente `ISaleFinalizer` dans Infrastructure. Il transforme une préparation Application en vente, lignes, allocations, paiement, mouvements Inventory, soldes décrémentés et opération idempotente dans une transaction SQL Server unique en isolation `Serializable`.

## Ordre transactionnel

1. valider la requête et le paiement ;
2. calculer l’empreinte SHA-256 normalisée ;
3. ouvrir la transaction ;
4. résoudre un éventuel rejeu par `OperationId` ;
5. charger produits, paramètres locaux, emplacements, lots et balances ;
6. revalider activation, vendabilité et prix snapshot ;
7. exclure stock nul, emplacement inactif et lots expirés ;
8. planifier FEFO puis vérifier la quantité totale ;
9. enregistrer `SaleOperation` en cours ;
10. créer vente, lignes, allocations, paiement et mouvements `Sale` ;
11. décrémenter les balances suivies par `rowversion` ;
12. marquer l’opération terminée et committer.

Tout échec avant le commit annule l’ensemble. Aucun objet métier définitif n’est écrit lors d’un refus de stock ou de prix.

## FEFO

Les balances sont triées par date de péremption croissante, puis les lots sans date, puis identifiants de lot, emplacement et balance. Un lot expiré n’est jamais alloué. Une ligne peut produire plusieurs allocations et autant de mouvements de sortie.

## Idempotence

L’empreinte couvre brouillon, version, lignes triées, snapshots, quantités, prix, total et paiement. Un même `OperationId` avec la même empreinte recharge la vente persistée. Une empreinte différente retourne `sales.operation.conflict`. Les identifiants de paiement et mouvements sont dérivés de manière déterministe.

## Concurrence

`StockBalances.RowVersion` empêche le last-write-wins. Les exceptions EF de concurrence et les erreurs SQL Server 1205, 1222 et 3960 sont converties en `sales.stock.concurrency_conflict` dans la transaction afin qu’elles ne soient pas rejouées automatiquement. La nouvelle tentative est laissée à l’utilisateur après revalidation du panier.

## Défaillances sûres

Les indisponibilités de connexion sont converties en `sales.database.unavailable`. Les violations uniques sont résolues par lecture de l’opération gagnante, sans seconde consommation. Aucun détail de connexion ou message SQL brut ne remonte au POS.

## Lectures

`SqlSalesReader` utilise `AsNoTracking`, requêtes séparées pour le graphe et projections bornées. Les snapshots persistent indépendamment des changements futurs du Catalogue.

## Composition

`AddSqlSalesAdapters` enregistre le finalizer, le lecteur et l’horloge derrière les contrats Application. Les brouillons et préparations restent les seuls états Sales en mémoire.
