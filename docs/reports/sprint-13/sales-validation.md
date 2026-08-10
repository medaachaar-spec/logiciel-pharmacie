# Sprint 13 — Guide de validation manuelle Sales

Statut automatisé : `PASS — MANUAL POS/SSMS REVIEW REQUIRED`

## Préparation sûre

Utiliser uniquement `Pharmacy_Development` et les données synthétiques. Noter la référence produit, l’emplacement, le lot et la quantité avant chaque scénario. Ne jamais modifier une balance directement pour fabriquer une vente.

Lancer l’application avec une connexion fournie au processus, puis ouvrir Ventes, Stock et Catalogue. Ouvrir SSMS sur les tables `sales.Sales`, `SaleLines`, `SaleLineAllocations`, `Payments`, `SaleOperations` et `inventory.StockMovements`.

## Scénarios

1. Vendre en espèces un produit disposant d’un seul lot ; vérifier référence `VTE-*`, paiement, mouvement `Sale`, balance diminuée et ligne d’historique.
2. Vendre une quantité répartie sur deux lots ; vérifier le lot non expiré le plus proche en premier, deux allocations et deux mouvements pour la quantité exacte.
3. Demander plus que le stock vendable ; vérifier le refus, le panier conservé et l’absence de vente, paiement, mouvement ou débit.
4. Refuser puis confirmer une carte externe ; seule la confirmation doit créer une vente, sans donnée bancaire.
5. Double-cliquer sur Valider ; vérifier une seule vente, un paiement, une série d’allocations et un seul débit.
6. Simuler l’échec d’impression après succès, puis réimprimer ; vérifier que vente et stock ne changent plus.
7. Placer un produit dans le panier, changer son prix dans Catalogue, puis valider ; vérifier `sales.price.changed`, aucun débit et panier conservé.
8. Couper SQL pendant la validation ; vérifier le message sûr, aucun écran de succès et panier conservé.

## Captures requises

- POS avant validation ;
- succès avec référence persistée ;
- Stock avant/après et mouvements `Sale` ;
- historique récent ;
- allocations multi-lots FEFO ;
- refus stock insuffisant ;
- échec d’impression après commit ;
- tables Sales dans SSMS ;
- résultat `PASS` de `verify_sprint13_sales.sql`.

## Vérification SQL

```powershell
sqlcmd -S <instance-explicite> -d Pharmacy_Development -E -C -b -f 65001 `
  -i .\database\verification\verify_sprint13_sales.sql
```

Le script est en lecture seule, refuse toute autre base et accepte une base sans vente manuelle. Dès qu’une vente existe, il contrôle cohérence des totaux, opération, paiement, allocations, mouvements et balances.
