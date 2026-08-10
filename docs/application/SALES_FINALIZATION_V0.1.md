# Finalisation des ventes Application V0.1

Date : 2026-08-10

Statut : `IMPLEMENTED — AUTOMATED CHECKS PASS — MANUAL POS REVIEW REQUIRED`

## Objectif

La couche Application orchestre désormais une finalisation persistante sans connaître EF Core ni SQL Server. Elle conserve les brouillons en mémoire, prépare leur snapshot puis délègue une seule requête immuable à `ISaleFinalizer`.

## Chaîne de commande

`FinalizeSaleHandler` reçoit `OperationId`, identifiant/version du brouillon et choix de paiement. Il refuse d’abord un identifiant vide, appelle `PrepareSaleFinalizationHandler`, valide le paiement puis transmet `FinalizeSaleRequest` au finalizer.

La préparation relit la disponibilité pour chaque ligne et ne modifie jamais le brouillon. Un échec de paiement ou de persistance laisse donc le panier et sa version disponibles pour correction ou nouvelle tentative.

## Contrats

- `ISaleFinalizer` finalise atomiquement et retourne `FinalizedSaleView` ;
- `ISalesReader` charge une vente par identifiant et les ventes récentes ;
- `ISalesClock` fournit la date métier et l’instant UTC aux adaptateurs ;
- `ISaleFinalizationPreparationStore` reste un cache de préparation local au processus, pas un registre de ventes.

Les vues retournées contiennent l’identifiant et la référence persistés, les snapshots de lignes, les allocations de lots et le paiement. Aucun type EF ou Infrastructure n’est exposé.

## Paiements V0.1

- `Cash` exige un montant reçu supérieur ou égal au total ; la monnaie est calculée avant délégation ;
- `ExternalCard` exige une confirmation explicite du TPE externe ; aucun montant reçu ni monnaie ne s’applique ;
- aucun paiement mixte et aucune donnée carte ne sont acceptés.

## Lectures

`GetSaleByIdHandler` et `GetRecentSalesHandler` valident leurs paramètres avant d’appeler `ISalesReader`. La liste récente est bornée à 50 lignes.

## Erreurs stables

- `sales.finalization.empty_draft` ;
- `sales.operation.invalid` ;
- `sales.operation.conflict` ;
- `sales.stock.insufficient` ;
- `sales.stock.concurrency_conflict` ;
- `sales.price.changed` ;
- `sales.payment.invalid` ;
- `sales.payment.card_unconfirmed` ;
- `sales.database.unavailable` ;
- `sales.sale.not_found` ;
- `sales.sale.query.invalid`.

Les erreurs historiques des mutations de brouillon restent inchangées, notamment `sales.stock.insufficient_availability`.

## Limites

Les brouillons et suspensions restent locaux au processus. Le sprint ne couvre ni annulation, retour, avoir, paiement mixte, caisse, client, ordonnance, fidélité, périphérique TPE ou impression réelle.
