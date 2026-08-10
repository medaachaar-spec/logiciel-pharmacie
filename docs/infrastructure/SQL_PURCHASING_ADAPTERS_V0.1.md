# SQL Purchasing Adapters V0.1

## Composition

- `SqlPurchasingReader` : projections en lecture seule, recherche Catalogue achetable et diagnostic.
- `SqlPurchasingStore` : mutations fournisseurs/commandes et transaction de réception.
- `SystemPurchasingClock` : instants UTC et date métier injectables.
- `AddSqlPurchasingAdapters` : enregistrement DI sans connexion, migration ou seed automatique.

Les entités EF sont `internal` dans Infrastructure. Le Desktop ne reçoit que les handlers Application.

## Transaction de réception

Sous stratégie d’exécution EF et isolation `Serializable`, l’adaptateur :

1. recherche un replay par `OperationId` ;
2. valide fournisseur, emplacement, commande, produits, quantités, lots et péremptions ;
3. crée ou réutilise les lots ;
4. insère la réception ;
5. crée une balance si nécessaire et l’augmente ;
6. crée un mouvement `PurchaseReceipt` par ligne ;
7. écrit les lignes de réception et snapshots ;
8. met à jour quantités et statut de commande ;
9. commit.

Toute erreur avant le commit déclenche un rollback total. Les conflits métier/concurrence ne sont pas rejoués aveuglément.

## Idempotence et concurrence

L’empreinte SHA-256 normalise le contenu fonctionnel. Les `OperationId` de mouvements/lots dérivés sont déterministes. Les index uniques protègent l’opération, le BL par fournisseur et le mouvement. Les `rowversion` sont encodées en Base64 dans Application.

## Lots et prix

Un lot existant doit appartenir au produit. Un nouveau lot est réutilisé par produit/numéro si sa péremption concorde. Les produits suivis exigent lot/péremption selon leur configuration. Une péremption antérieure à la date métier de réception est refusée par prudence. Le prix d’achat `decimal(19,4)` est un snapshot ; le prix de vente Catalogue ne change jamais automatiquement.
